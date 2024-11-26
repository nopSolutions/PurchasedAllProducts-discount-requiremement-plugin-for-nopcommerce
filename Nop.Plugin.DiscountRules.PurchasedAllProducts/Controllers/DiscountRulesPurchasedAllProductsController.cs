using Microsoft.AspNetCore.Mvc;
using Nop.Core.Domain.Discounts;
using Nop.Plugin.DiscountRules.PurchasedAllProducts.Models;
using Nop.Services.Catalog;
using Nop.Services.Configuration;
using Nop.Services.Discounts;
using Nop.Services.Security;
using Nop.Web.Areas.Admin.Factories;
using Nop.Web.Areas.Admin.Models.Catalog;
using Nop.Web.Framework;
using Nop.Web.Framework.Controllers;
using Nop.Web.Framework.Mvc.Filters;

namespace Nop.Plugin.DiscountRules.PurchasedAllProducts.Controllers;

[AuthorizeAdmin]
[Area(AreaNames.ADMIN)]
[AutoValidateAntiforgeryToken]
public class DiscountRulesPurchasedAllProductsController : BasePluginController
{
    #region Fields

    private const char _idsSeparator = ',';
    private readonly IDiscountService _discountService;
    private readonly IProductModelFactory _productModelFactory;
    private readonly IProductService _productService;
    private readonly ISettingService _settingService;

    #endregion

    #region Ctor

    public DiscountRulesPurchasedAllProductsController(IDiscountService discountService,
        IProductModelFactory productModelFactory,
        IProductService productService,
        ISettingService settingService)
    {
        _discountService = discountService;
        _productModelFactory = productModelFactory;
        _productService = productService;
        _settingService = settingService;
    }

    #endregion

    #region Utilities

    private IEnumerable<string> GetErrorsFromModelState()
    {
        return ModelState.Values.SelectMany(v => v.Errors.Select(e => e.ErrorMessage));
    }

    #endregion

    #region Methods

    [CheckPermission(StandardPermission.Promotions.DISCOUNTS_VIEW)]
    public async Task<IActionResult> Configure(int discountId, int? discountRequirementId)
    {
        var discount = await _discountService.GetDiscountByIdAsync(discountId) ?? throw new ArgumentException("Discount could not be loaded");

        //check whether the discount requirement exists
        if (discountRequirementId.HasValue && await _discountService.GetDiscountRequirementByIdAsync(discountRequirementId.Value) is null)
            return Content("Failed to load requirement.");

        var restrictedProductVariantIds = await _settingService.GetSettingByKeyAsync<string>(string.Format(DiscountRequirementDefaults.SETTINGS_KEY, discountRequirementId ?? 0));

        var model = new RequirementModel
        {
            RequirementId = discountRequirementId ?? 0,
            DiscountId = discount.Id,
            ProductIds = restrictedProductVariantIds
        };

        //add a prefix
        ViewData.TemplateInfo.HtmlFieldPrefix = string.Format(DiscountRequirementDefaults.HTML_FIELD_PREFIX, discountRequirementId ?? 0);

        return View("~/Plugins/DiscountRules.PurchasedAllProducts/Views/Configure.cshtml", model);
    }

    [HttpPost]
    [CheckPermission(StandardPermission.Promotions.DISCOUNTS_CREATE_EDIT_DELETE)]
    public async Task<IActionResult> Configure(RequirementModel model)
    {
        if (ModelState.IsValid)
        {
            //load the discount
            var discount = await _discountService.GetDiscountByIdAsync(model.DiscountId);
            if (discount == null)
                return NotFound(new { Errors = new[] { "Discount could not be loaded" } });

            //get the discount requirement
            var discountRequirement = await _discountService.GetDiscountRequirementByIdAsync(model.RequirementId);

            //the discount requirement does not exist, so create a new one
            if (discountRequirement == null)
            {
                discountRequirement = new DiscountRequirement
                {
                    DiscountId = discount.Id,
                    DiscountRequirementRuleSystemName = DiscountRequirementDefaults.SYSTEM_NAME
                };

                await _discountService.InsertDiscountRequirementAsync(discountRequirement);
            }

            //save restricted customer role identifier
            await _settingService.SetSettingAsync(string.Format(DiscountRequirementDefaults.SETTINGS_KEY, discountRequirement.Id), model.ProductIds);

            return Ok(new { NewRequirementId = discountRequirement.Id });
        }

        return BadRequest(new { Errors = GetErrorsFromModelState() });
    }


    [CheckPermission(StandardPermission.Catalog.PRODUCTS_VIEW)]
    public async Task<IActionResult> ProductAddPopup(string btnId, string productIdsInput)
    {
        ViewBag.productIdsInput = productIdsInput;
        ViewBag.btnId = btnId;

        //prepare model
        var model = await _productModelFactory.PrepareProductSearchModelAsync(new ProductSearchModel());

        return View("~/Plugins/DiscountRules.PurchasedAllProducts/Views/ProductAddPopup.cshtml", model);
    }

    [HttpPost]
    [CheckPermission(StandardPermission.Catalog.PRODUCTS_VIEW)]
    public async Task<IActionResult> LoadProductFriendlyNames(string productIds)
    {
        if (string.IsNullOrWhiteSpace(productIds))
            return Json(new { Text = string.Empty });

        var parsedIds = new List<int>();
        var idsArray = productIds
            .Split(_idsSeparator, StringSplitOptions.RemoveEmptyEntries)
            .Select(x => x.Trim())
            .ToList();

        foreach (var strId in idsArray)
        {
            if (int.TryParse(strId, out var parsedId))
                parsedIds.Add(parsedId);
        }

        var products = await _productService.GetProductsByIdsAsync(parsedIds.ToArray());
        return Json(new { Text = string.Join(", ", products.Select(p => p.Name)) });
    }

    #endregion
}