﻿using Laser.Orchard.HiddenFields.Security;
using Laser.Orchard.HiddenFields.Services;
using Orchard.ContentManagement;
using Orchard.ContentManagement.Aspects;
using Orchard.Security;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Web;

namespace Laser.Orchard.HiddenFields.Handlers {
    public class HiddenStringFieldAuthorizationEventHandler : IAuthorizationServiceEventHandler {
        private readonly IHiddenFieldService _hiddenFieldService;

        public HiddenStringFieldAuthorizationEventHandler(IHiddenFieldService hiddenFieldService) {
            _hiddenFieldService = hiddenFieldService;
        }

        public void Adjust(CheckAccessContext context) {
            if (!context.Granted && context.Permission is HiddenFieldEditPermission) {
                var fieldPermission = context.Permission as HiddenFieldEditPermission;
                // qui dovrò controllare anche GetSeeOwn e GetSeeAll
                if (HasOwnership(context.User, context.Content)) {
                    // Own Permission.
                    context.Permission = _hiddenFieldService.GetOwnPermission(fieldPermission.Part, fieldPermission.Field);
                    context.Adjusted = true;
                }
                else {
                    // All Permission.
                    context.Permission = _hiddenFieldService.GetAllPermission(fieldPermission.Part, fieldPermission.Field);
                    context.Adjusted = true;
                }
            }
        }

        public void Checking(CheckAccessContext context) {

        }

        public void Complete(CheckAccessContext context) {

        }

        private static bool HasOwnership(IUser user, IContent content) {
            if (user == null || content == null)
                return false;

            var common = content.As<ICommonPart>();
            if (common == null || common.Owner == null)
                return false;

            return user.Id == common.Owner.Id;
        }
    }
}