using System;
using System.Collections.Generic;
using System.Linq;
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

        #endregion

        #region Ctor

        public PurchasedAllProductsDiscountRequirementRule(ISettingService settingService, 
            IRepository<OrderItem> orderItemRepository)
        {
            this._settingService = settingService;
            this._orderItemRepository = orderItemRepository;
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
                throw new ArgumentNullException("request");

            //invalid by default
            var result = new DiscountRequirementValidationResult();

            var restrictedProductVariantIdsStr = _settingService.GetSettingByKey<string>(string.Format("DiscountRequirement.RestrictedProductVariantIds-{0}", request.DiscountRequirementId));

            if (String.IsNullOrWhiteSpace(restrictedProductVariantIdsStr))
            {
                result.IsValid = true;
                return result;
            }

            if (request.Customer == null)
                return result;

            var restrictedProductIds = new List<int>();

            try
            {
                restrictedProductIds = restrictedProductVariantIdsStr
                    .Split(new char[] { ',' }, StringSplitOptions.RemoveEmptyEntries)
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
            var orderStatusId = (int)OrderStatus.Complete;
            //purchased product
            var purchasedProducts = _orderItemRepository.Table.Where(oi => oi.Order.CustomerId == customerId && !oi.Order.Deleted && oi.Order.OrderStatusId == orderStatusId).ToList();

            bool allFound = restrictedProductIds
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
            //configured in RouteProvider.cs
            string result = "Plugins/DiscountRulesPurchasedAllProducts/Configure/?discountId=" + discountId;

            if (discountRequirementId.HasValue)
                result += string.Format("&discountRequirementId={0}", discountRequirementId.Value);

            return result;
        }

        /// <summary>
        /// Install plugin
        /// </summary>
        public override void Install()
        {
            //locales
            this.AddOrUpdatePluginLocaleResource("Plugins.DiscountRules.PurchasedAllProducts.Fields.ProductVariants", "Restricted product variants");
            this.AddOrUpdatePluginLocaleResource("Plugins.DiscountRules.PurchasedAllProducts.Fields.ProductVariants.Hint", "The comma-separated list of product variant identifiers (e.g. 77, 123, 156). You can find a product variant ID on its details page.");
            base.Install();
        }

        /// <summary>
        /// Uninstall plugin
        /// </summary>
        public override void Uninstall()
        {
            //locales
            this.DeletePluginLocaleResource("Plugins.DiscountRules.PurchasedAllProducts.Fields.ProductVariants");
            this.DeletePluginLocaleResource("Plugins.DiscountRules.PurchasedAllProducts.Fields.ProductVariants.Hint");
            base.Uninstall();
        }

        #endregion
    }
}