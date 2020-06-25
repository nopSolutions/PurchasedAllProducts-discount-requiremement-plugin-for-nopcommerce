using System;
using System.Collections.Generic;
using System.Linq;
using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.Mvc.Infrastructure;
using Microsoft.AspNetCore.Mvc.Routing;
using Nop.Core;
using Nop.Core.Domain.Orders;
using Nop.Services.Configuration;
using Nop.Services.Discounts;
using Nop.Services.Localization;
using Nop.Services.Orders;
using Nop.Services.Plugins;

namespace Nop.Plugin.DiscountRules.PurchasedAllProducts
{
    public partial class PurchasedAllProductsDiscountRequirementRule : BasePlugin, IDiscountRequirementRule
    {
        #region Fields

        private readonly IActionContextAccessor _actionContextAccessor;
        private readonly IDiscountService _discountService;
        private readonly ILocalizationService _localizationService;
        private readonly IReturnRequestService _returnRequestService;
        private readonly IOrderService _orderService;
        private readonly ISettingService _settingService;
        private readonly IUrlHelperFactory _urlHelperFactory;
        private readonly IWebHelper _webHelper;

        #endregion

        #region Ctor

        public PurchasedAllProductsDiscountRequirementRule(IActionContextAccessor actionContextAccessor,
            IDiscountService discountService,
            ILocalizationService localizationService,
            IReturnRequestService returnRequestService,
            IOrderService orderService,
            ISettingService settingService,
            IUrlHelperFactory urlHelperFactory,
            IWebHelper webHelper
            )
        {
            _actionContextAccessor = actionContextAccessor;
            _discountService = discountService;
            _localizationService = localizationService;
            _returnRequestService = returnRequestService;
            _orderService = orderService;
            _settingService = settingService;
            _urlHelperFactory = urlHelperFactory;
            _webHelper = webHelper;
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

            var restrictedProductVariantIdsStr = _settingService.GetSettingByKey<string>(string.Format(DiscountRequirementDefaults.SETTINGS_KEY, request.DiscountRequirementId));

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

            //get available orders
            var availableOrders = _orderService.SearchOrders(
                customerId: customerId,
                osIds: new List<int> { (int)OrderStatus.Complete });

            //get all available purchased product ids
            var purchasedProductIds = availableOrders
                .SelectMany(order => _orderService.GetOrderItems(order.Id))
                .Where(orderItem =>
                {
                    //exclude products from return requests
                    var returnRequests = _returnRequestService
                        .SearchReturnRequests(customerId: customerId, orderItemId: orderItem.Id)
                        .Where(returnRequest => returnRequest.ReturnRequestStatus != ReturnRequestStatus.Cancelled &&
                                                  returnRequest.ReturnRequestStatus != ReturnRequestStatus.RequestRejected &&
                                                    returnRequest.ReturnRequestStatus != ReturnRequestStatus.ItemsRepaired);
                    var returnedQuantity = 0;
                    foreach (var returnRequest in returnRequests)
                        returnedQuantity += returnRequest.Quantity;

                    return returnedQuantity < orderItem.Quantity;
                })
                .Select(orderItem => orderItem.ProductId)
                .Distinct();

            //check if all purchased products are match the restricted products
            result.IsValid = restrictedProductIds
                .All(productId => purchasedProductIds.Any(purchasedProductId => purchasedProductId == productId));

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

            return urlHelper.Action("Configure", "DiscountRulesPurchasedAllProducts",
                new { discountId = discountId, discountRequirementId = discountRequirementId }, _webHelper.CurrentRequestProtocol);
        }

        /// <summary>
        /// Install plugin
        /// </summary>
        public override void Install()
        {
            //locales
            _localizationService.AddPluginLocaleResource(new Dictionary<string, string>
            {
                ["Plugins.DiscountRules.PurchasedAllProducts.Fields.Products"] = "Restricted products",
                ["Plugins.DiscountRules.PurchasedAllProducts.Fields.Products.Hint"] = "The comma-separated list of product identifiers (e.g. 77, 123, 156). You can find a product ID on its details page.",
                ["Plugins.DiscountRules.PurchasedAllProducts.Fields.Products.AddNew"] = "Add product",
                ["Plugins.DiscountRules.PurchasedAllProducts.Fields.Products.Choose"] = "Choose",
                ["Plugins.DiscountRules.PurchasedAllProducts.Fields.ProductIds.Required"] = "Products are required",
                ["Plugins.DiscountRules.PurchasedAllProducts.Fields.DiscountId.Required"] = "Discount is required",
                ["Plugins.DiscountRules.PurchasedAllProducts.Fields.ProductIds.InvalidFormat"] = "Invalid format of the products selection. Format should be comma-separated list of product identifiers (e.g. 77, 123, 156). You can find a product ID on its details page."
            });

            base.Install();
        }

        /// <summary>
        /// Uninstall plugin
        /// </summary>
        public override void Uninstall()
        {
            //discount requirements
            var discountRequirements = _discountService.GetAllDiscountRequirements()
                .Where(discountRequirement => discountRequirement.DiscountRequirementRuleSystemName == DiscountRequirementDefaults.SYSTEM_NAME);
            foreach (var discountRequirement in discountRequirements)
            {
                _discountService.DeleteDiscountRequirement(discountRequirement, false);
            }

            //locales
            _localizationService.DeletePluginLocaleResources("Plugins.DiscountRules.PurchasedAllProducts");

            base.Uninstall();
        }

        #endregion
    }
}