﻿using System.Collections.Generic;
using Laser.Orchard.StartupConfig.ShortCodes.Abstractions;
using Orchard;

namespace Laser.Orchard.StartupConfig.ShortCodes.Abstractions {
    public interface IShortCodesServices : IDependency {
        IEnumerable<IShortCodeProvider> GetProviders();
        IEnumerable<IShortCodeProvider> GetEnabledProviders(DescribeContext context); 
    }
}