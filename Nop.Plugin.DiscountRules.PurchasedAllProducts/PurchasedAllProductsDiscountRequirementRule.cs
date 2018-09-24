using System;
using System.Collections.Generic;
using System.Linq;
using Microsoft.AspNetCore.Http;
using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.Mvc.Infrastructure;
using Microsoft.AspNetCore.Mvc.Routing;
using Nop.Core.Data;
using Nop.Core.Domain.Orders;
using Nop.Core.Plugins;
using Nop.Services.Configuration;
using Nop.Services.Discounts;
using Nop.Services.Localization;

namespace Nop.Plugin.DiscountRules.PurchasedAllProducts
{
    public partial class PurchasedAllProductsDiscountRequirementRule : BasePlugin, IDiscountRequirementRule
    {
        #region Fields

        private readonly ISettingService _settingService;
        private readonly IRepository<OrderItem> _orderItemRepository;
        private readonly IActionContextAccessor _actionContextAccessor;
        private readonly IUrlHelperFactory _urlHelperFactory;
        private readonly ILocalizationService _localizationService;

        #endregion

        #region Ctor

        public PurchasedAllProductsDiscountRequirementRule(ISettingService settingService, 
            IRepository<OrderItem> orderItemRepository,
            IActionContextAccessor actionContextAccessor,
            IUrlHelperFactory urlHelperFactory,
            ILocalizationService localizationService
            )

        {
            this._settingService = settingService;
            this._orderItemRepository = orderItemRepository;
            this._actionContextAccessor = actionContextAccessor;
            this._urlHelperFactory = urlHelperFactory;
            this._localizationService = localizationService;
        }

        #endregion

        #region Methods

        /// <summary>
        /// Check discount requirement
        /// </summary>
        /// <param name="request">Object that contains all information required to check the requirement (Current customer, discount, etc)</param>
        /// <returns>Result</returns>
        public DiscountRequirementValidationResult CheckRequirement(DiscountRequirementValidationRequest request)
        {
            if (request == null)
                throw new ArgumentNullException(nameof(request));

            //invalid by default
            var result = new DiscountRequirementValidationResult();

            var restrictedProductVariantIdsStr = _settingService.GetSettingByKey<string>($"DiscountRequirement.RestrictedProductVariantIds-{request.DiscountRequirementId}");

            if (string.IsNullOrWhiteSpace(restrictedProductVariantIdsStr))
            {
                result.IsValid = true;
                return result;
            }

            if (request.Customer == null)
                return result;

            List<int> restrictedProductIds;

            try
            {
                restrictedProductIds = restrictedProductVariantIdsStr
                    .Split(new[] { ',' }, StringSplitOptions.RemoveEmptyEntries)
                    .Select(x => Convert.ToInt32(x))
                    .ToList();
            }
            catch
            {
                //error parsing
                return result;
            }

            if (restrictedProductIds.Count == 0)
                return result;

            var customerId = request.Customer.Id;
            const int orderStatusId = (int)OrderStatus.Complete;
            //purchased product
            var purchasedProducts = _orderItemRepository.Table.Where(oi => oi.Order.CustomerId == customerId && !oi.Order.Deleted && oi.Order.OrderStatusId == orderStatusId).ToList();

            var allFound = restrictedProductIds
                .Select(restrictedProductId => purchasedProducts.Any(purchasedProduct => restrictedProductId == purchasedProduct.ProductId))
                .All(found1 => found1);

            if (allFound)
            {
                result.IsValid = true;
                return result;
            }

            return result;
        }

        /// <summary>
        /// Get URL for rule configuration
        /// </summary>
        /// <param name="discountId">Discount identifier</param>
        /// <param name="discountRequirementId">Discount requirement identifier (if editing)</param>
        /// <returns>URL</returns>
        public string GetConfigurationUrl(int discountId, int? discountRequirementId)
        {
            var urlHelper = _urlHelperFactory.GetUrlHelper(_actionContextAccessor.ActionContext);
            var url = new PathString(urlHelper.Action("Configure", "DiscountRulesPurchasedAllProducts",
            new { discountId = discountId, discountRequirementId = discountRequirementId }));

            //remove the application path from the generated URL if exists
            var pathBase = _actionContextAccessor.ActionContext?.HttpContext?.Request?.PathBase ?? PathString.Empty;
            url.StartsWithSegments(pathBase, out url);

            return url.Value.TrimStart('/');
        }

        /// <summary>
        /// Install plugin
        /// </summary>
        public override void Install()
        {
            //locales
            _localizationService.AddOrUpdatePluginLocaleResource("Plugins.DiscountRules.PurchasedAllProducts.Fields.Products", "Restricted products");
            _localizationService.AddOrUpdatePluginLocaleResource("Plugins.DiscountRules.PurchasedAllProducts.Fields.Products.Hint", "The comma-separated list of product identifiers (e.g. 77, 123, 156). You can find a product ID on its details page.");
            _localizationService.AddOrUpdatePluginLocaleResource("Plugins.DiscountRules.PurchasedAllProducts.Fields.Products.AddNew", "Add product");
            _localizationService.AddOrUpdatePluginLocaleResource("Plugins.DiscountRules.PurchasedAllProducts.Fields.Products.Choose", "Choose");

            base.Install();
        }

        /// <summary>
        /// Uninstall plugin
        /// </summary>
        public override void Uninstall()
        {
            //locales
            _localizationService.DeletePluginLocaleResource("Plugins.DiscountRules.PurchasedAllProducts.Fields.Products");
            _localizationService.DeletePluginLocaleResource("Plugins.DiscountRules.PurchasedAllProducts.Fields.Products.Hint");
            _localizationService.DeletePluginLocaleResource("Plugins.DiscountRules.PurchasedAllProducts.Fields.Products.AddNew");
            _localizationService.DeletePluginLocaleResource("Plugins.DiscountRules.PurchasedAllProducts.Fields.Products.Choose");

            base.Uninstall();
        }

        #endregion
    }
}