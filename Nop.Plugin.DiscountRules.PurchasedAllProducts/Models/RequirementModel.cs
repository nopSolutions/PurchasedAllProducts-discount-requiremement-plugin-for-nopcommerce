using Nop.Web.Framework.Mvc.ModelBinding;

namespace Nop.Plugin.DiscountRules.PurchasedAllProducts.Models
{
    public record RequirementModel
    {
        public int DiscountId { get; set; }

        [NopResourceDisplayName("Plugins.DiscountRules.PurchasedAllProducts.Fields.Products")]
        public string ProductIds { get; set; }

        public int RequirementId { get; set; }
    }
}