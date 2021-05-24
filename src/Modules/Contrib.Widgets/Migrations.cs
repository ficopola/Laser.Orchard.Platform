﻿using Contrib.Widgets.Models;
using Orchard.ContentManagement;
using Orchard.ContentManagement.MetaData;
using Orchard.Core.Contents.Extensions;
using Orchard.Data.Migration;
using Orchard.Environment.Extensions;
using Orchard.Widgets.Models;
using Orchard.Widgets.Services;
using System.Linq;

namespace Contrib.Widgets {
    [OrchardFeature("Contrib.Widgets")]
    public class Migrations : DataMigrationImpl {
        private readonly IContentManager _contentManager;
        private readonly IWidgetsService _widgetsService;

        public Migrations(
            IContentManager contentManager, 
            IWidgetsService widgetsService) {
            _contentManager = contentManager;
            _widgetsService = widgetsService;
        }

        public int Create() {

            SchemaBuilder.CreateTable("WidgetExPartRecord", table => table
                .ContentPartRecord()
                .Column<int>("HostId"));

            ContentDefinitionManager.AlterPartDefinition("WidgetExPart", part => part.Attachable(false));

            ContentDefinitionManager.AlterPartDefinition("WidgetsContainerPart", part => part
                .Attachable()
                .WithSetting("ContentPartSettings.Description", "Enables content items to contain widgets, removing the need to create a layer rule per content item."));

            ContentDefinitionManager.AlterTypeDefinition("WidgetsPage", type => type
                .WithPart("CommonPart", p => p
                    .WithSetting("DateEditorSettings.ShowDateEditor", "true"))
                .WithPart("PublishLaterPart")
                .WithPart("TitlePart")
                .WithPart("AutoroutePart", part => part
                    .WithSetting("AutorouteSettings.AllowCustomPattern", "true")
                    .WithSetting("AutorouteSettings.AutomaticAdjustmentOnEdit", "false")
                    .WithSetting("AutorouteSettings.PatternDefinitions", "[{Name:'Title', Pattern: '{Content.Slug}', Description: 'my-page'}]")
                    .WithSetting("AutorouteSettings.DefaultPatternIndex", "0"))
                .WithPart("BodyPart")
                .WithPart("WidgetsContainerPart")
                .Creatable());

            return 2;
        }

        public int UpdateFrom1() {
            ContentDefinitionManager.AlterTypeDefinition("WidgetsPage", type => type
                .WithPart("CommonPart", p => p
                    .WithSetting("DateEditorSettings.ShowDateEditor", "true"))
                .WithPart("PublishLaterPart")
                .WithPart("TitlePart")
                .WithPart("AutoroutePart", part => part
                    .WithSetting("AutorouteSettings.AllowCustomPattern", "true")
                    .WithSetting("AutorouteSettings.AutomaticAdjustmentOnEdit", "false")
                    .WithSetting("AutorouteSettings.PatternDefinitions", "[{Name:'Title', Pattern: '{Content.Slug}', Description: 'my-page'}]")
                    .WithSetting("AutorouteSettings.DefaultPatternIndex", "0"))
                .WithPart("BodyPart")
                .WithPart("WidgetsContainerPart")
                .Creatable());

            return 2;
        }

        public int UpdateFrom2() {
            ContentDefinitionManager.AlterTypeDefinition("WidgetsPage", type => type.Listable());
            return 3;
        }

        public int UpdateFrom3() {
            var widgets = _contentManager
                .Query<WidgetExPart, WidgetExPartRecord>(VersionOptions.Latest)
                .List();

            foreach (var widget in widgets) {
                var widgetPart = widget.As<WidgetPart>();
                if (widgetPart != null && widgetPart.ContentItem.VersionRecord != null && !widgetPart.ContentItem.VersionRecord.Published) {
                    _contentManager.Publish(widget.ContentItem);
                }
            }

            return 4;
        }

        public int UpdateFrom4() {
            if(!_widgetsService.GetLayers().Any(x => x.Name == "ContentWidgets")) {
                _widgetsService.CreateLayer("ContentWidgets", "This layer never activates, but is needed for the widgets hosted by content items for now.", "false");
            }
            return 5;
        }
    }
}