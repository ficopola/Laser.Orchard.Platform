﻿using Laser.Orchard.AdvancedSettings.Models;
using Orchard;
using Orchard.Caching;
using Orchard.ContentManagement;
using Orchard.ContentManagement.Handlers;
using Orchard.Logging;
using Orchard.MediaLibrary.Fields;
using System;
using System.Collections.Generic;
using System.Linq;

namespace Laser.Orchard.AdvancedSettings.Services {
    public class AdvancedSettingsService : IAdvancedSettingsService {
        private readonly IContentManager _contentManager;
        private readonly ICacheManager _cacheManager;
        private readonly ISignals _signals;
        private readonly Lazy<IEnumerable<IContentHandler>> _handlers;

        public AdvancedSettingsService(IContentManager contentManager, 
            ICacheManager cacheManager, 
            ISignals signals,
            Lazy<IEnumerable<IContentHandler>> handlers) {
            _contentManager = contentManager;
            _cacheManager = cacheManager;
            _signals = signals;
            _handlers = handlers;
            Logger = NullLogger.Instance;
        }
        public ILogger Logger { get; set; }
        public IEnumerable<IContentHandler> Handlers {
            get { return _handlers.Value; }
        }

        public string SettingsCacheKey(string settingName) {
            return string.Format("AdvancedSettings-{0}-Cache", settingName);
        }

        public IContent GetCachedSetting(string settingName) {
            var advancedSettings = _cacheManager.Get(SettingsCacheKey(settingName), true, context => {
                context.Monitor(_signals.When(SettingsCacheKey(settingName)));
                var settings = _contentManager
                    .Query<AdvancedSettingsPart, AdvancedSettingsPartRecord>()
                    .Where(x => x.Name.Equals(settingName, StringComparison.InvariantCultureIgnoreCase))
                    .Slice(0, 1)
                    .SingleOrDefault();
                // MediaLibraryPickerFields have their MediaParts property as a Lazy<T>. We force its enumeration
                // here to attempt to avoid NHibernate exceptions that would be caused by attemtping to enumerate
                // it in parallel.
                if (settings != null) {
                    foreach (var mlpf in settings.ContentItem.Parts.SelectMany(x => x.Fields.OfType<MediaLibraryPickerField>())) {
                        mlpf.MediaParts.ToList();
                    }
                }
                return settings?.ContentItem;
            });
            // rehydrate ContentManager to prevent expired lifetime scopes
            if (advancedSettings != null) {
                advancedSettings.ContentManager = _contentManager;
            }

            // create a context with a new instance to load            
            var context = new LoadContentContext(advancedSettings);
            // invoke handlers to acquire state, or at least establish lazy loading callbacks
            Handlers.Invoke(handler => handler.Loading(context), Logger);
            Handlers.Invoke(handler => handler.Loaded(context), Logger);

            return advancedSettings;
        }

        public void ReleaseCachedSetting(string settingName) {
            _signals.Trigger(SettingsCacheKey(settingName));
        }
    }
}
