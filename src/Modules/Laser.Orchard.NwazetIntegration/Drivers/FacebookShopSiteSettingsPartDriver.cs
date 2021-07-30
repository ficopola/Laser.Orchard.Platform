﻿using Laser.Orchard.NwazetIntegration.Models;
using Laser.Orchard.NwazetIntegration.Services;
using Laser.Orchard.NwazetIntegration.Services.FacebookShop;
using Laser.Orchard.NwazetIntegration.ViewModels;
using Orchard.ContentManagement;
using Orchard.ContentManagement.Drivers;
using Orchard.Environment.Extensions;
using Orchard.Localization;
using Orchard.Security;

namespace Laser.Orchard.NwazetIntegration.Drivers {
    [OrchardFeature("Laser.Orchard.FacebookShop")]
    public class FacebookShopSiteSettingsPartDriver : ContentPartDriver<FacebookShopSiteSettingsPart> {
        private readonly IAuthorizer _authorizer;
        private readonly IFacebookShopService _facebookShopService;

        protected override string Prefix => "FacebookShopSiteSettingsPart";

        public FacebookShopSiteSettingsPartDriver(IAuthorizer authorizer, IFacebookShopService facebookShopService) {
            _authorizer = authorizer;
            _facebookShopService = facebookShopService;

            T = NullLocalizer.Instance;
        }

        public Localizer T { get; set; }

        protected override DriverResult Editor(FacebookShopSiteSettingsPart part, dynamic shapeHelper) {
            return ContentShape("SiteSettings_FacebookShop",
                () => shapeHelper.EditorTemplate(
                    TemplateName: "SiteSettings/FacebookShopSiteSettings",
                    Model: CreateVM(part),
                    Prefix: Prefix)).OnGroup("FacebookShop");
        }

        protected override DriverResult Editor(FacebookShopSiteSettingsPart part, IUpdateModel updater, dynamic shapeHelper) {
            var viewModel = CreateVM(part);
            if (updater.TryUpdateModel(viewModel, Prefix, null, null)) {
                if (Validate(viewModel, part, updater)) {
                    part.Save(viewModel);
                } else {
                    return ContentShape("SiteSettings_FacebookShop", 
                        () => shapeHelper.EditorTemplate(
                        TemplateName: "SiteSettings/FacebookShopSiteSettings",
                        Model: viewModel,
                        Prefix: Prefix)).OnGroup("FacebookShop");
                }
            }

            return Editor(part, shapeHelper);
        }

        private FacebookShopSiteSettingsViewModel CreateVM(FacebookShopSiteSettingsPart part) {
            return new FacebookShopSiteSettingsViewModel() {
                ApiBaseUrl = part.ApiBaseUrl,
                DefaultJsonForProductUpdate = part.DefaultJsonForProductUpdate,
                BusinessId = part.BusinessId,
                CatalogId = part.CatalogId,
                AppId = part.AppId,
                AppSecret = part.AppSecret
            };
        }

        private bool Validate(FacebookShopSiteSettingsViewModel viewModel, FacebookShopSiteSettingsPart part, IUpdateModel updater) {
            if (string.IsNullOrWhiteSpace(viewModel.ApiBaseUrl)) {
                updater.AddModelError(Prefix, T("Api base url is required."));
                return false;
            }

            if (string.IsNullOrWhiteSpace(viewModel.AppId)) {
                updater.AddModelError(Prefix, T("App Id is required."));
                return false;
            }

            if (string.IsNullOrWhiteSpace(viewModel.AppSecret)) {
                updater.AddModelError(Prefix, T("App Secret is required."));
                return false;
            }

            var serviceContext = FacebookShopServiceContext.From(viewModel);
            
            if (string.IsNullOrWhiteSpace(viewModel.BusinessId) || !_facebookShopService.CheckBusiness(serviceContext)) {
                updater.AddModelError(Prefix, T("Invalid business id."));
                return false;
            }

            if (string.IsNullOrWhiteSpace(viewModel.CatalogId) || !_facebookShopService.CheckCatalog(serviceContext)) {
                updater.AddModelError(Prefix, T("Invalid catalog id."));
                return false;
            }

            return true;
        }
    }
}