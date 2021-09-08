﻿using Orchard.Environment.Extensions;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Web;

namespace Laser.Orchard.AdvancedSettings.ViewModels {
    [OrchardFeature("Laser.Orchard.ThemeSkins")]
    public class ThemeSkinsPartEditViewModel {
        public string SkinName { get; set; }
    }
}