﻿using Orchard;
using Orchard.ContentManagement;
using Orchard.ContentManagement.Handlers;
using Orchard.Environment.Configuration;
using Orchard.Environment.Extensions;
using Orchard.OutputCache.Models;
using Orchard.OutputCache.Services;
using Orchard.Roles.Events;
using Orchard.Roles.Models;
using System;
using System.Collections;
using System.Collections.Generic;
using System.Linq;
using System.Web;

namespace Laser.Orchard.StartupConfig.CacheByRole.Handlers {
    [OrchardFeature("Laser.Orchard.StartupConfig.CacheByRole")]
    public class CacheByRoleHandler : IRoleEventHandler {
        private readonly string _tenantName;
        private readonly WorkContext _workContext;
        private readonly IOutputCacheStorageProvider _cacheStorageProvider;

        // used to manage the role evict once 
        // the role evict
        private string roleEvict = string.Empty;

        public CacheByRoleHandler(
            IWorkContextAccessor workContextAccessor,
            IOutputCacheStorageProvider cacheStorageProvider,
            ShellSettings shellSettings) {
            _workContext = workContextAccessor.GetContext();
            _cacheStorageProvider = cacheStorageProvider;
            _tenantName = shellSettings.Name;
        }
        public void PermissionAdded(PermissionAddedContext context) {
            if(roleEvict != context.Role.Name) {
                roleEvict = context.Role.Name;
                EvictCache(context.Role.Name);
            }
        }

        public void PermissionRemoved(PermissionRemovedContext context) {
            if (roleEvict != context.Role.Name) {
                roleEvict = context.Role.Name;
                EvictCache(context.Role.Name);
            }
        }

        public void Removed(RoleRemovedContext context) {
            EvictCache(context.Role.Name);
        }

        public void Created(RoleCreatedContext context) {
        }

        public void Renamed(RoleRenamedContext context) {
        }

        public void UserAdded(UserAddedContext context) {
        }

        public void UserRemoved(UserRemovedContext context) {
        }

        private void EvictCache(string role) {
           var cacheItems = _workContext.HttpContext.Cache.AsParallel()
                .Cast<DictionaryEntry>()
                .Select(x => x.Value)
                .OfType<CacheItem>()
                .Where(x => x.Tenant.Equals(_tenantName, StringComparison.OrdinalIgnoreCase)
                        && x.CacheKey.Contains(role));

            foreach (var item in cacheItems) {
                _cacheStorageProvider.Remove(item.CacheKey);
            }
        }
    }
}