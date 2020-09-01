﻿using Laser.Orchard.NwazetIntegration.Models;
using Laser.Orchard.NwazetIntegration.ViewModels;
using Nwazet.Commerce.Models;
using Nwazet.Commerce.Services;
using Orchard.ContentManagement;
using Orchard.ContentManagement.Drivers;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Web;

namespace Laser.Orchard.NwazetIntegration.Drivers {
    /// <summary>
    /// Insert a shape in products' editor views to allow users to input
    /// the price after tax, and have the taxable price be computed in place
    /// based on the VAT rate (for the default territory).
    /// </summary>
    public class ProductVatConfigurationPartDriver : ContentPartDriver<ProductVatConfigurationPart> {

        private readonly IVatConfigurationService _vatConfigurationService;
        private readonly IVatConfigurationProvider _vatConfigurationProvider;
        private readonly IProductPriceService _productPriceService;

        public ProductVatConfigurationPartDriver(
            IVatConfigurationService vatConfigurationService,
            IVatConfigurationProvider vatConfigurationProvider,
            IProductPriceService productPriceService) {

            _vatConfigurationService = vatConfigurationService;
            _vatConfigurationProvider = vatConfigurationProvider;
            _productPriceService = productPriceService;
        }

        protected override string Prefix {
            get { return "ProductVatConfigurationPart"; }
        }

        protected override DriverResult Editor(ProductVatConfigurationPart part, dynamic shapeHelper) {
            var settings = part.Settings.GetModel<ProductVatConfigurationPartInputPriceSettings>();

            var model = new ProductPriceEditorViewModel();
            if (settings != null && settings.InputFinalPrice) {
                model = CreateVM(part);
            }
            return ContentShape("Parts_ProductPriceWithVAT_Edit",
                () => shapeHelper.EditorTemplate(
                    TemplateName: "Parts/ProductPriceWithVATEditor",
                    Model: model,
                    Prefix: Prefix
                    ));
        }

        protected override DriverResult Editor(ProductVatConfigurationPart part, IUpdateModel updater, dynamic shapeHelper) {
            // this is here so that if something errors out in some other driver,
            // we are still displaying the shape correctly
            return Editor(part, shapeHelper);
        }

        public ProductPriceEditorViewModel CreateVM(ProductVatConfigurationPart part) {
            var productPart = part.As<ProductPart>();

            var vatRates = _vatConfigurationProvider
                .GetVatConfigurations()
                .Select(vcp => new {
                    id = vcp.Id,
                    rate = _vatConfigurationService.GetRate(vcp)
                })
                .ToDictionary(a => a.id, a => a.rate);
            vatRates.Add(0, _vatConfigurationService.GetRate(_vatConfigurationService.GetDefaultCategory()));

            if (productPart != null) {
                return new ProductPriceEditorViewModel(part, productPart, _productPriceService) {

                    SelectedVatConfigurationId = part.UseDefaultVatCategory
                        ? 0
                        : part.VatConfigurationPart.Record.Id,
                    DefaultVatConfigurationId = _vatConfigurationService
                        .GetDefaultCategoryId(),
                    DefaultTerritoryName = _vatConfigurationService
                        .GetDefaultDestination()
                        ?.Name ?? string.Empty,
                    VatRates = vatRates
                };
            }
            // if it's not a product, it may be a shipping method
            var shippingPart = part.As<IShippingMethod>();
            // NOTE: VAT configuration currently only works properly and is tested
            // for FlexibleShippingMethodPart. Other IShippingMethod implementations
            // likely don't work with it.
            if (shippingPart != null && shippingPart is FlexibleShippingMethodPart) {
                return new ProductPriceEditorViewModel(part, shippingPart, _vatConfigurationService) {

                    SelectedVatConfigurationId = part.UseDefaultVatCategory
                        ? 0
                        : part.VatConfigurationPart.Record.Id,
                    DefaultVatConfigurationId = _vatConfigurationService
                        .GetDefaultCategoryId(),
                    DefaultTerritoryName = _vatConfigurationService
                        .GetDefaultDestination()
                        ?.Name ?? string.Empty,
                    VatRates = vatRates
                };
            }
            // could not identify any price thing
            return null;
        }
    }
}