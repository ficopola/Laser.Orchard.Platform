﻿using Laser.Orchard.StartupConfig.Services.ContentSerialization;
using Markdown.Services;
using Newtonsoft.Json;
using Newtonsoft.Json.Linq;
using Orchard;
using Orchard.ContentManagement;
using Orchard.Core.Common.Fields;
using Orchard.Core.Contents;
using Orchard.Fields.Fields;
using Orchard.Localization.Models;
using Orchard.Localization.Services;
using Orchard.Projections.Services;
using Orchard.Taxonomies.Fields;
using Orchard.Taxonomies.Models;
using Orchard.Taxonomies.Services;
using System;
using System.Collections;
using System.Collections.Generic;
using System.Linq;
using System.Reflection;

namespace Laser.Orchard.StartupConfig.Services {
    public interface IContentSerializationServices : IDependency {
        JObject GetJson(IContent content, int page = 1, int pageSize = 10, string filter = "");
        JObject Terms(IContent content, int maxLevel = 10);
        void NormalizeSingleProperty(JObject json);
    }

    public class ContentSerializationServices : IContentSerializationServices {
        private readonly IOrchardServices _orchardServices;
        private readonly IProjectionManager _projectionManager;
        private readonly ITaxonomyService _taxonomyService;
        private readonly ILocalizationService _localizationService;
        private readonly IEnumerable<ISpecificContentFieldSerializationProvider> _contentFieldSerializationProviders;

        private readonly MarkdownFilter _markdownFilter;

        private readonly string[] _skipAlwaysProperties;
        private readonly string _skipAlwaysPropertiesEndWith;
        private readonly string[] _skipFieldProperties;
        private readonly string[] _skipFieldTypes;
        private readonly string[] _skipPartNames;
        private readonly string[] _skipPartProperties;
        private readonly string[] _skipPartTypes;

        private readonly Type[] _basicTypes;

        private int _maxLevel = 10;

        private List<ProcessedObject> processedItems;

        private string[] _filter;

        public ContentSerializationServices(IOrchardServices orchardServices,
            IProjectionManager projectionManager, 
            ITaxonomyService taxonomyService,
            ILocalizationService localizationService,
            IEnumerable<ISpecificContentFieldSerializationProvider> contentFieldSerializationProviders) {

            _orchardServices = orchardServices;
            _projectionManager = projectionManager;
            _taxonomyService = taxonomyService;
            _localizationService = localizationService;
            _contentFieldSerializationProviders = contentFieldSerializationProviders;
            _markdownFilter = new MarkdownFilter();

            _skipAlwaysProperties = new string[] { "ContentItemRecord", "ContentItemVersionRecord", "TermsPartRecord", "UserPolicyPartRecord" };
            _skipAlwaysPropertiesEndWith = "Proxy";
            _skipFieldProperties = new string[] { "Storage", "Name", "DisplayName", "Setting" };
            _skipFieldTypes = new string[] { "FieldDefinition", "PartFieldDefinition" };
            _skipPartNames = new string[] { "InfosetPart", "FieldIndexPart", "IdentityPart", "UserPart", "UserRolesPart", "AdminMenuPart", "MenuPart", "TaxonomyPart", "TermsPart" };
            _skipPartProperties = new string[] { "CommentedOnContentItem", "CommentedOnContentItemMetadata", "PendingComments" };
            _skipPartTypes = new string[] { "ContentItem", "Zones", "TypeDefinition", "TypePartDefinition", "PartDefinition", "Settings", "Fields", "Record" };

            _basicTypes = new Type[] {
                typeof(string),
                typeof(decimal),
                typeof(float),
                typeof(int),
                typeof(bool),
                typeof(DateTime),
                typeof(Enum)
            };

            // initialize ContentField serialization providers with the configurazion for this serialization.
            var settings = new SerializationSettings {
                SkipAlwaysProperties = _skipAlwaysProperties,
                SkipAlwaysPropertiesEndWith = _skipAlwaysPropertiesEndWith,
                SkipFieldProperties=_skipFieldProperties,
                SkipFieldTypes= _skipFieldTypes,
                SkipPartNames= _skipPartNames,
                SkipPartProperties= _skipPartProperties,
                SkipPartTypes= _skipPartTypes,
                BasicTypes= _basicTypes,
                MaxLevel= _maxLevel,
                // backreferences
                ContentSerializationService = this,
                SerializerFactory = JsonSerializerInstance,
                ObjectSerializerMethod = SerializeObject
            };
            foreach (var p in _contentFieldSerializationProviders) {
                p.Configure(settings);
            }

            processedItems = new List<ProcessedObject>();
        }

        #region IContentSerializationServices Implementation

        public JObject Terms(IContent content, int maxLevel = 10) {
            JObject json;
            _maxLevel = maxLevel;
            dynamic contentToSerialize = null, termPart = null;
            try {
                if (content.ContentItem.ContentType.EndsWith("Taxonomy")) {
                    contentToSerialize = content;
                    json = new JObject(SerializeObject(content, 0, 0, new string[] { "TaxonomyPart" }));
                    var terms = content.As<TaxonomyPart>().Terms;
                    var resultArray = new JArray();
                    foreach (var resulted in terms) {
                        resultArray.Add(new JObject(SerializeObject(resulted.ContentItem, 0, content.Id)));
                    }
                    JObject taxonomy = json.First.First as JObject; //The Taxonomy node
                    taxonomy.Add("Terms", resultArray);
                    //NormalizeSingleProperty(json);
                    return json;

                } else if (content.ContentItem.ContentType.EndsWith("Term") || !string.IsNullOrWhiteSpace(content.ContentItem.TypeDefinition.Settings["Taxonomy"])) {
                    termPart = ((dynamic)content.ContentItem).TermPart;
                    if (termPart != null) {
                        json = new JObject(SerializeObject(content, 0, 0));
                        contentToSerialize = _taxonomyService.GetChildren(termPart, false);
                        var resultArray = new JArray();
                        foreach (var resulted in contentToSerialize) {
                            resultArray.Add(new JObject(SerializeObject(resulted.ContentItem, 0, termPart.Id)));
                        }
                        JObject rootTerm = json.First.First as JObject; //The first term node
                        rootTerm.Add("SubTerms", resultArray);
                        return json;
                    }
                }
            } catch {
            }
            return null;
        }

        public JObject GetJson(IContent content, int page = 1, int pageSize = 10, string filter = "") {
            JObject json;
            // BuildDisplay populates the TagCache properly with the Id of the content
            // It impacts performances but we have to choose if to call the BuildDisplay 
            // or to manually populate the Tag cache with the content Id
            // Invoking BuilDisplay also ensures all handlers are correctly invoked for the content:
            // this ensures some rare conditions are correctly handled, e.g. FieldExternal
            _orchardServices.ContentManager.BuildDisplay(content);
            var filteredContent = content;
            _filter = filter.ToLower()
                .Split(new char[] { ',' }, StringSplitOptions.RemoveEmptyEntries)
                .Select(x => x.EndsWith(".") ? x : x + ".").ToArray();

            json = new JObject(SerializeObject(filteredContent, 0, 0));
            dynamic part;

            #region [Projections]
            // Projection
            try {
                part = ((dynamic)filteredContent).ProjectionPart;
            } catch {
                part = null;
            }
            if (part != null) {
                var queryId = part.Record.QueryPartRecord.Id;
                var queryItems = _projectionManager.GetContentItems(queryId, (page - 1) * pageSize, pageSize);
                var resultArray = new JArray();
                foreach (var resulted in queryItems) {
                    // BuildDisplay populates the TagCache properly with the Id of the content
                    // It impacts performances but we have to choose if to call the BuildDisplay 
                    // or to manually populate the Tag cache with the content Id
                    _orchardServices.ContentManager.BuildDisplay((ContentItem)resulted);
                    resultArray.Add(new JObject(SerializeObject((ContentItem)resulted, 1, part.Id)));
                }
                if ((json.Root.HasValues) && (json.Root.First.HasValues) && (json.Root.First.First.HasValues)) {
                    JProperty array = new JProperty("ProjectionPageResults", resultArray);
                    json.Root.First.First.Last.AddAfterSelf(array);
                } else {
                    json.Add("ProjectionPageResults", resultArray);
                }
            }
            #endregion

            //NormalizeSingleProperty(json);

            return json;
        }

        /// <summary>
        /// Accorpa gli oggetti che hanno una sola proprietà con la proprietà padre.
        /// Es. Creator: { Value: 2 } diventa Creator: 2.
        /// </summary>
        /// <param name="json"></param>
        public void NormalizeSingleProperty(JObject json) {
            List<JToken> nodeList = new List<JToken>();

            // scandisce tutto l'albero dei nodi e salva i nodi potenzialmente "interessanti" in una lista
            nodeList.Add(json.Root);
            for (int i = 0; i < nodeList.Count; i++) {
                foreach (var tempNode in nodeList[i].Children()) {
                    if (tempNode.HasValues) {
                        nodeList.Add(tempNode);
                    }
                }
            }

            // scorre tutti i nodi per cercare quelli da accorpare
            foreach (var tempNode in nodeList) {
                if (tempNode.Count() == 1) {
                    if (tempNode.First.HasValues == false) {
                        if ((tempNode.Parent != null) && (tempNode.Parent.Count == 1)) {
                            if ((tempNode.Parent.Parent != null)
                                && (tempNode.Parent.Parent.Count == 1)
                                && (tempNode.Parent.Parent.Type == JTokenType.Property)) {
                                (tempNode.Parent.Parent as JProperty).Value = tempNode.First;
                            }
                        }
                    }
                }
            }
        }

        #endregion

        private JProperty SerializeObject(
            object item, int actualLevel, int parentContentId, string[] skipProperties = null) {

            JProperty aux = null;
            if ((actualLevel + 1) > _maxLevel) {
                if (((dynamic)item).Id != null) {
                    return new JProperty(item.GetType().Name, new JObject(new JProperty("Id", ((dynamic)item).Id)));
                } else {
                    return new JProperty(item.GetType().Name, null);
                }
            }
            try {
                if (item.GetType().Name.EndsWith(_skipAlwaysPropertiesEndWith)) {
                    return new JProperty(item.GetType().Name, null);
                }
            } catch {
            }
            skipProperties = skipProperties ?? new string[0];
            try {
                if (item is ContentPart &&
                    parentContentId != ((dynamic)item).Id) {
                    return SerializeObject((ContentItem)((dynamic)item).ContentItem, actualLevel, parentContentId, skipProperties);
                }

                if (item.GetType().GetProperties().Count(x => x.Name == "Id") > 0) {
                    PopulateProcessedItems(item.GetType().Name, ((dynamic)item).Id, parentContentId);
                }
                if (item is ContentPart) {
                    return SerializePart((ContentPart)item, actualLevel, ((ContentPart)item).Id);
                } else if (item is ContentField) {
                    return SerializeField((ContentField)item, actualLevel); //, ((ContentPart)item).Id);
                } else if (item is ContentItem) {
                    return SerializeContentItem((ContentItem)item, actualLevel + 1, parentContentId);
                } else if (typeof(IEnumerable).IsInstanceOfType(item)) { // Lista o array
                    JArray array = new JArray();
                    foreach (var itemArray in (item as IEnumerable)) {
                        if (IsBasicType(itemArray.GetType())) {
                            var valItem = itemArray;
                            FormatValue(ref valItem);
                            array.Add(valItem);
                        } else {
                            aux = SerializeObject(itemArray, actualLevel + 1, parentContentId, skipProperties);
                            array.Add(new JObject(aux));
                        }
                    }
                    return new JProperty(item.GetType().Name, array);
                } else if (item.GetType().IsClass) {
                    var members = item.GetType()
                        .GetFields(BindingFlags.Instance | BindingFlags.Public)
                        .Cast<MemberInfo>()
                        .Union(item.GetType().GetProperties(BindingFlags.Instance | BindingFlags.Public))
                        .Where(m => !skipProperties.Contains(m.Name)
                            && !_skipAlwaysProperties.Contains(m.Name)
                            && !m.Name.EndsWith(_skipAlwaysPropertiesEndWith));
                    List<JProperty> properties = new List<JProperty>();
                    foreach (var member in members) {
                        var propertyInfo = item.GetType().GetProperty(member.Name);
                        object val = item.GetType().GetProperty(member.Name).GetValue(item);
                        if (IsBasicType(propertyInfo.PropertyType)) {
                            var memberVal = val;
                            FormatValue(ref memberVal);
                            properties.Add(new JProperty(member.Name, memberVal));
                        } else if (typeof(IEnumerable).IsInstanceOfType(val)) {
                            JArray arr = new JArray();
                            properties.Add(new JProperty(member.Name, arr));
                            foreach (var element in (val as IEnumerable)) {
                                if (IsBasicType(element.GetType())) {
                                    var valItem = element;
                                    FormatValue(ref valItem);
                                    arr.Add(valItem);
                                } else {
                                    aux = SerializeObject(element, actualLevel + 1, parentContentId, skipProperties);
                                    arr.Add(new JObject(aux));
                                }
                            }
                        } else {
                            aux = SerializeObject(propertyInfo.GetValue(item), actualLevel + 1, parentContentId, skipProperties);
                            properties.Add(aux);
                        }
                    }
                    return new JProperty(item.GetType().Name, new JObject(properties));

                    //JObject propertiesObject;
                    //var serializer = JsonSerializerInstance();
                    //propertiesObject = JObject.FromObject(item, serializer);
                    //foreach (var skip in skipProperties) {
                    //    propertiesObject.Remove(skip);
                    //}
                    //PopulateProcessedItems(item.GetType().Name, ((dynamic)item).Id);
                    //return new JProperty(item.GetType().Name, propertiesObject);
                } else {
                    return new JProperty(item.GetType().Name, item);
                }
            } catch (Exception ex) {
                return new JProperty(item.GetType().Name, ex.Message);
            }
        }

        public JProperty SerializeContentItem(
            ContentItem item, int actualLevel, int parentContentId) {
            if ((actualLevel + 1) > _maxLevel) {
                return new JProperty("ContentItem", null);
            }
            JProperty jsonItem;
            var jsonProps = new JObject(
                new JProperty("Id", item.Id),
                new JProperty("Version", item.Version));

            var partsObject = new JObject();

            var parts = item.Parts
                .Where(cp => !cp.PartDefinition.Name.Contains("`")
                    && !_skipPartNames.Contains(cp.PartDefinition.Name)
                    && (_filter == null ||
                        _filter.Length == 0 ||
                        (cp!=null && cp.ContentItem != null && (
                        _filter.Contains(cp.ContentItem.ContentType + ".", StringComparer.InvariantCultureIgnoreCase)) /*All parts for a specific content (es: BlogPost.)*/||
                        _filter.Contains(cp.ContentItem.ContentType + "." + cp.PartDefinition.Name + ".", StringComparer.InvariantCultureIgnoreCase) /*A specific part for all the contents (es: .TitlePart.)*/||
                        _filter.Contains("." + cp.PartDefinition.Name + ".", StringComparer.InvariantCultureIgnoreCase) /*A Specific Part for a specific content (es: BlogPost.TitlePart.)*/
                )));
            foreach (var part in parts) {
                jsonProps.Add(SerializePart(part, actualLevel + 1, item.Id, item));
            }

            jsonItem = new JProperty(item.ContentType,
                jsonProps
                );

            return jsonItem;
        }

        private JProperty SerializePart(
            ContentPart part, int actualLevel, int parentContentId, ContentItem item = null) {
            // Part properties
            var properties = part.GetType().GetProperties(BindingFlags.Instance | BindingFlags.Public).Where(prop =>
                !_skipPartTypes.Contains(prop.Name) //skip 
                );
            var partObject = new JObject();
            foreach (var property in properties) {
                // skip "Id" property if it has the same value of the part's container content item id.
                if (property.Name == "Id" && item != null && part.Id == item.Id) {
                    continue;
                }
                try {
                    if (!_skipPartProperties.Contains(property.Name)) {
                        object val = property.GetValue(part, BindingFlags.GetProperty, null, null, null);
                        if (val != null) {
                            PopulateJObject(ref partObject, property, val, _skipPartProperties, actualLevel, parentContentId);
                        }
                    }
                }
                catch { }
            }

            // now add the fields to the json object
            foreach (var contentField in part.Fields) {
                // Checking ContentFieldSerializationSettings to see if the field needs to be serialized.
                var serializeField = true;
                if (contentField.PartFieldDefinition.Settings.ContainsKey("ContentFieldSerializationSettings.AllowSerialization")) {
                    bool.TryParse(
                        contentField.PartFieldDefinition.Settings["ContentFieldSerializationSettings.AllowSerialization"], 
                        out serializeField);
                }

                if (serializeField) {
                    var fieldObject = SerializeField(contentField, actualLevel, part, item);
                    partObject.Add(fieldObject);
                }
            }

            try {
                if (part.GetType() == typeof(ContentPart) && !part.PartDefinition.Name.EndsWith("Part")) {
                    return new JProperty(part.PartDefinition.Name + "DPart", partObject);
                }
                else {
                    return new JProperty(part.PartDefinition.Name, partObject);
                }
            }
            catch {
                // We never really want to end up here, because this is not "repeatable", meaning
                // that if for some content we end here, its serialization will always be different.
                return new JProperty(Guid.NewGuid().ToString(), partObject);
            }
        }

        #region Serialization of ContentFields
        private JProperty SerializeField(
            ContentField field, int actualLevel, ContentPart part = null, ContentItem item = null) {
            var fieldObject = new JObject();

            switch (field.FieldDefinition.Name.ToLowerInvariant()) {
                // Treating these fields as simple values is how mobile apps worked as of now, so they're go on working this way for now.
                case "numericfield":
                case "textfield":
                case "inputfield":
                case "booleanfield":
                case "datetimefield":
                    return SerializeValueField(field, actualLevel, part, item);

                default:
                    return SerializeObjectField(field, actualLevel, part, item);
            }
        }

        private JProperty SerializeValueField(
            ContentField field, int actualLevel, ContentPart part = null, ContentItem item = null) {
            if (field.FieldDefinition.Name == "NumericField") {
                var numericField = field as NumericField;
                object val = 0;
                if (numericField.Value.HasValue) {
                    val = numericField.Value.Value;
                }
                FormatValue(ref val);
                return new JProperty(field.Name + field.FieldDefinition.Name, val);
            } else if (field.FieldDefinition.Name == "TextField") {
                var textField = field as TextField;
                object val = textField.Value;
                if (val != null) {
                    if (textField.PartFieldDefinition.Settings.ContainsKey("TextFieldSettings.Flavor")) {
                        var flavor = textField.PartFieldDefinition.Settings["TextFieldSettings.Flavor"];
                        // markdownFilter acts only if flavor is "markdown"
                        val = _markdownFilter.ProcessContent(val.ToString(), flavor);
                    }
                    FormatValue(ref val);
                }
                return new JProperty(field.Name + field.FieldDefinition.Name, val);
            } else if (field.FieldDefinition.Name == "InputField") {
                var inputField = field as InputField;
                object val = inputField.Value;
                FormatValue(ref val);
                return new JProperty(field.Name + field.FieldDefinition.Name, val);
            } else if (field.FieldDefinition.Name == "BooleanField") {
                var booleanField = field as BooleanField;
                object val = false;
                if (booleanField.Value.HasValue) {
                    val = booleanField.Value.Value;
                }
                FormatValue(ref val);
                return new JProperty(field.Name + field.FieldDefinition.Name, val);
            } else if (field.FieldDefinition.Name == "DateTimeField") {
                var dateTimeField = field as DateTimeField;
                object val = dateTimeField.DateTime;
                FormatValue(ref val);
                return new JProperty(field.Name + field.FieldDefinition.Name, val);
            } else {
                // TODO: serialize the field like it's a generic name-value field.
                // This code is never executed because the routine is called for specific fields only.
                return null;
            }
        }

        private JProperty SerializeObjectField(
            ContentField field, int actualLevel, ContentPart part = null, ContentItem item = null) {

            var fieldObject = new JObject();
            // find if the field has providers dedicated to its own serialization:
            var specificProviders = _contentFieldSerializationProviders.Where(p => p.IsSpecificForField(field));

            var fieldClassName = field.FieldDefinition.Name;
            if (specificProviders.Any()) {
                var mostSpecificProvider = specificProviders
                    .OrderByDescending(p => p.Specificity)
                    .FirstOrDefault();
                fieldClassName = mostSpecificProvider.ComputeFieldClassName(field, part, item);
            }

            fieldObject.Add("ContentFieldClassName", JToken.FromObject(fieldClassName));
            fieldObject.Add("ContentFieldTechnicalName", JToken.FromObject(field.Name));
            fieldObject.Add("ContentFieldDisplayName", JToken.FromObject(field.DisplayName));

            // if that is the case, they will handle serialization
            if (specificProviders.Any()) {
                foreach (var provider in specificProviders) {
                    provider.PopulateJObject(ref fieldObject, field, actualLevel, item);
                }
            } else {
                // otherwise, we have a generic serialization step that uses reflection
                var properties = field.GetType().GetProperties(BindingFlags.Instance | BindingFlags.Public).Where(prop =>
                    !_skipFieldTypes.Contains(prop.Name) //skip 
                    );

                foreach (var property in properties) {
                    try {
                        if (!_skipFieldProperties.Contains(property.Name) &&
                                !CustomAttributeData.GetCustomAttributes(property).Any(x => x.AttributeType == typeof(JsonIgnoreAttribute))) {
                            object val = property.GetValue(field, BindingFlags.GetProperty, null, null, null);
                            if (val != null) {
                                PopulateJObject(ref fieldObject, property, val, _skipFieldProperties, actualLevel, item?.Id ?? 0);
                            }
                        }
                    } catch {

                    }
                }
            }

            return new JProperty(field.Name + field.FieldDefinition.Name, fieldObject);
        }
        #endregion
        private void PopulateJObject(
            ref JObject jObject, 
            PropertyInfo property, object val, string[] skipProperties, int actualLevel, int parentContentId) {

            JObject propertiesObject;
            var serializer = JsonSerializerInstance();
            if (val is Array || val.GetType().IsGenericType) {
                JArray array = new JArray();
                foreach (var itemArray in (IEnumerable)val) {

                    if (!IsBasicType(itemArray.GetType())) {
                        var aux = SerializeObject(itemArray, actualLevel, parentContentId, skipProperties);
                        array.Add(new JObject(aux));
                    }
                    else {
                        var valItem = itemArray;
                        FormatValue(ref valItem);
                        array.Add(valItem);
                    }
                }
                jObject.Add(new JProperty(property.Name, array));

            }
            else if (val is ContentItem) {
                var contentProperty = SerializeObject(val, actualLevel, parentContentId);
                jObject.Add(new JProperty(property.Name, new JObject(contentProperty)));
            }
            if (!IsBasicType(val.GetType())) {
                try {
                    propertiesObject = JObject.FromObject(val, serializer);
                    foreach (var skip in skipProperties) {
                        propertiesObject.Remove(skip);
                    }
                    jObject.Add(property.Name, propertiesObject);
                }
                catch {
                    jObject.Add(new JProperty(property.Name, val.GetType().FullName));
                }
            }
            else {
                FormatValue(ref val);
                jObject.Add(new JProperty(property.Name, val));
            }
        }

        #region Helper methos
        private bool IsBasicType(Type type) {
            return _basicTypes.Contains(type) || type.IsEnum;
        }

        private static void FormatValue(ref object val) {
            if (val != null && val.GetType().IsEnum) {
                val = val.ToString();
            }
        }

        private void PopulateProcessedItems(string key, dynamic id, int parentId) {
            if (id != null) {
                if (!processedItems.Any(x => 
                    x.Type == key 
                    && x.Id == id 
                    && x.ParentContentId == parentId)) {
                    processedItems.Add(
                        new ProcessedObject {
                            Id = id,
                            Type = key,
                            ParentContentId = parentId });
                }
            }
        }

        private JsonSerializer JsonSerializerInstance() {
            return new JsonSerializer {
                ReferenceLoopHandling = ReferenceLoopHandling.Ignore,
                DateFormatString = "#MM-dd-yyyy hh.mm.ss#",
            };
        }
        #endregion
    }

    class ProcessedObject {
        public int Id { get; set; }
        public string Type { get; set; }
        public int ParentContentId { get; set; }
    }
}