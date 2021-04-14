using System;
using System.Collections.Generic;
using System.Linq;
using System.Threading.Tasks;
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
        /// <returns>
        /// A task that represents the asynchronous operation
        /// The task result contains the result
        /// </returns>
        public async Task<DiscountRequirementValidationResult> CheckRequirementAsync(DiscountRequirementValidationRequest request)
        {
            if (request == null)
                throw new ArgumentNullException(nameof(request));

            //invalid by default
            var result = new DiscountRequirementValidationResult();

            var restrictedProductVariantIdsStr = await _settingService.GetSettingByKeyAsync<string>(string.Format(DiscountRequirementDefaults.SETTINGS_KEY, request.DiscountRequirementId));

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
            var availableOrders = await _orderService.SearchOrdersAsync(
                customerId: customerId,
                osIds: new List<int> { (int)OrderStatus.Complete });

            //get all available purchased product ids
            var purchasedProductIds = await availableOrders
                .SelectManyAwait(async order => await _orderService.GetOrderItemsAsync(order.Id))
                .WhereAwait(async orderItem =>
                {
                    //exclude products from return requests
                    var returnRequests = (await _returnRequestService.SearchReturnRequestsAsync(customerId: customerId, orderItemId: orderItem.Id))
                        .Where(returnRequest => returnRequest.ReturnRequestStatus != ReturnRequestStatus.Cancelled &&
                                                  returnRequest.ReturnRequestStatus != ReturnRequestStatus.RequestRejected &&
                                                    returnRequest.ReturnRequestStatus != ReturnRequestStatus.ItemsRepaired);
                    var returnedQuantity = 0;
                    foreach (var returnRequest in returnRequests)
                        returnedQuantity += returnRequest.Quantity;

                    return returnedQuantity < orderItem.Quantity;
                })
                .Select(orderItem => orderItem.ProductId)
                .Distinct()
                .ToListAsync();

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
                new { discountId = discountId, discountRequirementId = discountRequirementId }, _webHelper.GetCurrentRequestProtocol());
        }

        /// <summary>
        /// Install the plugin
        /// </summary>
        /// <returns>A task that represents the asynchronous operation</returns>
        public override async Task InstallAsync()
        {
            //locales
            await _localizationService.AddLocaleResourceAsync(new Dictionary<string, string>
            {
                ["Plugins.DiscountRules.PurchasedAllProducts.Fields.Products"] = "Restricted products",
                ["Plugins.DiscountRules.PurchasedAllProducts.Fields.Products.Hint"] = "The comma-separated list of product identifiers (e.g. 77, 123, 156). You can find a product ID on its details page.",
                ["Plugins.DiscountRules.PurchasedAllProducts.Fields.Products.AddNew"] = "Add product",
                ["Plugins.DiscountRules.PurchasedAllProducts.Fields.Products.Choose"] = "Choose",
                ["Plugins.DiscountRules.PurchasedAllProducts.Fields.ProductIds.Required"] = "Products are required",
                ["Plugins.DiscountRules.PurchasedAllProducts.Fields.DiscountId.Required"] = "Discount is required",
                ["Plugins.DiscountRules.PurchasedAllProducts.Fields.ProductIds.InvalidFormat"] = "Invalid format of the products selection. Format should be comma-separated list of product identifiers (e.g. 77, 123, 156). You can find a product ID on its details page."
            });

            await base.InstallAsync();
        }

        /// <summary>
        /// Uninstall the plugin
        /// </summary>
        /// <returns>A task that represents the asynchronous operation</returns>
        public override async Task UninstallAsync()
        {
            //discount requirements
            var discountRequirements = (await _discountService.GetAllDiscountRequirementsAsync())
                .Where(discountRequirement => discountRequirement.DiscountRequirementRuleSystemName == DiscountRequirementDefaults.SYSTEM_NAME);
            foreach (var discountRequirement in discountRequirements)
            {
                await _discountService.DeleteDiscountRequirementAsync(discountRequirement, false);
            }

            //locales
            await _localizationService.DeleteLocaleResourcesAsync("Plugins.DiscountRules.PurchasedAllProducts");

            await base.UninstallAsync();
        }

        #endregion
    }
}