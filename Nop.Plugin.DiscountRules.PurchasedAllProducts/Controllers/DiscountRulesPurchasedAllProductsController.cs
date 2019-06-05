using System;
using System.Collections.Generic;
using System.Linq;
using Microsoft.AspNetCore.Mvc;
using Nop.Core;
using Nop.Core.Domain.Discounts;
using Nop.Plugin.DiscountRules.PurchasedAllProducts.Models;
using Nop.Services.Catalog;
using Nop.Services.Configuration;
using Nop.Services.Discounts;
using Nop.Services.Localization;
using Nop.Services.Security;
using Nop.Services.Stores;
using Nop.Services.Vendors;
using Nop.Web.Areas.Admin.Factories;
using Nop.Web.Areas.Admin.Models.Catalog;
using Nop.Web.Framework;
using Nop.Web.Framework.Controllers;
using Nop.Web.Framework.Mvc.Filters;

namespace Nop.Plugin.DiscountRules.PurchasedAllProducts.Controllers
{
    [AuthorizeAdmin]
    [Area(AreaNames.Admin)]
    public class DiscountRulesPurchasedAllProductsController : BasePluginController
    {
        #region Fields

        private readonly ICategoryService _categoryService;
        private readonly IDiscountService _discountService;
        private readonly ILocalizationService _localizationService;
        private readonly IManufacturerService _manufacturerService;
        private readonly IPermissionService _permissionService;
        private readonly IProductModelFactory _productModelFactory;
        private readonly IProductService _productService;
        private readonly ISettingService _settingService;
        private readonly IStoreService _storeService;
        private readonly IVendorService _vendorService;
        private readonly IWorkContext _workContext;

        #endregion

        #region Ctor

        public DiscountRulesPurchasedAllProductsController(ICategoryService categoryService,
            IDiscountService discountService,
            ILocalizationService localizationService,
            IManufacturerService manufacturerService,
            IPermissionService permissionService,
            IProductModelFactory productModelFactory,
            IProductService productService,
            ISettingService settingService,
            IStoreService storeService,
            IVendorService vendorService,
            IWorkContext workContext)
        {
            _categoryService = categoryService;
            _discountService = discountService;
            _localizationService = localizationService;
            _manufacturerService = manufacturerService;
            _permissionService = permissionService;
            _productModelFactory = productModelFactory;
            _productService = productService;
            _settingService = settingService;
            _storeService = storeService;
            _vendorService = vendorService;
            _workContext = workContext;
        }

        #endregion

        #region Methods

        public IActionResult Configure(int discountId, int? discountRequirementId)
        {
            if (!_permissionService.Authorize(StandardPermissionProvider.ManageDiscounts))
                return Content("Access denied");

            var discount = _discountService.GetDiscountById(discountId);

            if (discount == null)
                throw new ArgumentException("Discount could not be loaded");

            if (discountRequirementId.HasValue)
            {
                var discountRequirement = discount.DiscountRequirements.FirstOrDefault(dr => dr.Id == discountRequirementId.Value);

                if (discountRequirement == null)
                    return Content("Failed to load requirement.");
            }

            var restrictedProductVariantIds = _settingService.GetSettingByKey<string>($"DiscountRequirement.RestrictedProductVariantIds-{discountRequirementId ?? 0}");

            var model = new RequirementModel
            {
                RequirementId = discountRequirementId ?? 0,
                DiscountId = discountId,
                Products = restrictedProductVariantIds
            };

            //add a prefix
            ViewData.TemplateInfo.HtmlFieldPrefix = $"DiscountRulesPurchasedAllProducts{discountRequirementId?.ToString() ?? "0"}";

            return View("~/Plugins/DiscountRules.PurchasedAllProducts/Views/Configure.cshtml", model);
        }

        [HttpPost]
        [AdminAntiForgery]
        public IActionResult Configure(int discountId, int? discountRequirementId, string variantIds)
        {
            if (!_permissionService.Authorize(StandardPermissionProvider.ManageDiscounts))
                return Content("Access denied");

            var discount = _discountService.GetDiscountById(discountId);

            if (discount == null)
                throw new ArgumentException("Discount could not be loaded");

            DiscountRequirement discountRequirement = null;

            if (discountRequirementId.HasValue)
                discountRequirement = discount.DiscountRequirements.FirstOrDefault(dr => dr.Id == discountRequirementId.Value);

            if (discountRequirement != null)
            {
                //update existing rule
                _discountService.UpdateDiscount(discount);
            }
            else
            {
                //save new rule
                discountRequirement = new DiscountRequirement()
                {
                    DiscountRequirementRuleSystemName = "DiscountRequirement.PurchasedAllProducts"
                };

                discount.DiscountRequirements.Add(discountRequirement);
                _discountService.UpdateDiscount(discount);

                _settingService.SetSetting($"DiscountRequirement.RestrictedProductVariantIds-{discountRequirement.Id}", variantIds);
            }

            return Json(new { Result = true, NewRequirementId = discountRequirement.Id });
        }

        public IActionResult ProductAddPopup(string btnId, string productIdsInput)
        {
            if (!_permissionService.Authorize(StandardPermissionProvider.ManageProducts))
                return AccessDeniedView();

            ViewBag.productIdsInput = productIdsInput;
            ViewBag.btnId = btnId;

            //prepare model
            var model = _productModelFactory.PrepareProductSearchModel(new ProductSearchModel());

            return View("~/Plugins/DiscountRules.PurchasedAllProducts/Views/ProductAddPopup.cshtml", model);
        }

        [HttpPost]
        [AdminAntiForgery]
        public IActionResult LoadProductFriendlyNames(string productIds)
        {
            var result = "";

            if (!_permissionService.Authorize(StandardPermissionProvider.ManageProducts))
                return Json(new { Text = result });

            if (string.IsNullOrWhiteSpace(productIds))
                return Json(new { Text = result });

            var ids = new List<int>();
            var idsArray = productIds
                .Split(new[] { ',' }, StringSplitOptions.RemoveEmptyEntries)
                .Select(x => x.Trim())
                .ToList();

            foreach (var str1 in idsArray)
            {
                if (int.TryParse(str1, out var tmp1))
                    ids.Add(tmp1);
            }

            var products = _productService.GetProductsByIds(ids.ToArray());
            for (var i = 0; i <= products.Count - 1; i++)
            {
                result += products[i].Name;
                if (i != products.Count - 1)
                    result += ", ";
            }

            return Json(new { Text = result });
        }

        #endregion
    }
}