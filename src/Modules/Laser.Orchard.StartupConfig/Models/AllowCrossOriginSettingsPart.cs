﻿using Orchard.ContentManagement;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Web;

namespace Laser.Orchard.StartupConfig.Models {
    public class AllowCrossOriginSettingsPart : ContentPart {

        public static string SettingsCacheKey = "AllowCrossOriginSettingsPartCacheKey";

        public bool RemoveXFrameHeaderBackEnd {
            get { return this.Retrieve(x => x.RemoveXFrameHeaderBackEnd, false); }
            set { this.Store(x => x.RemoveXFrameHeaderBackEnd, value); }
        }
        public bool RemoveXFrameHeaderFrontEnd {
            get { return this.Retrieve(x => x.RemoveXFrameHeaderFrontEnd, false); }
            set { this.Store(x => x.RemoveXFrameHeaderFrontEnd, value); }
        }
        public bool SetSameSiteNoneForAuthCookies {
            get { return this.Retrieve(x => x.SetSameSiteNoneForAuthCookies, false); }
            set { this.Store(x => x.SetSameSiteNoneForAuthCookies, value); }
        }
    }
}