using Microsoft.AspNetCore.Routing;
using Nop.Web.Framework.Mvc.Routing;
using Microsoft.AspNetCore.Builder;

namespace Nop.Plugin.DiscountRules.PurchasedAllProducts
{
    public partial class RouteProvider : IRouteProvider
    {
        #region Methods

        public void RegisterRoutes(IRouteBuilder routeBuilder)
        {
            routeBuilder.MapRoute("Plugin.DiscountRules.PurchasedAllProducts.Configure",
                 "Plugins/DiscountRulesPurchasedAllProducts/Configure",
                 new { controller = "DiscountRulesPurchasedAllProducts", action = "Configure" });

            routeBuilder.MapRoute("Plugin.DiscountRules.PurchasedAllProducts.ProductAddPopup",
                 "Plugins/DiscountRulesPurchasedAllProducts/ProductAddPopup",
                 new { controller = "DiscountRulesPurchasedAllProducts", action = "ProductAddPopup" });

            routeBuilder.MapRoute("Plugin.DiscountRules.PurchasedAllProducts.ProductAddPopupList",
                 "Plugins/DiscountRulesPurchasedAllProducts/ProductAddPopupList",
                 new { controller = "DiscountRulesPurchasedAllProducts", action = "ProductAddPopupList" });

            routeBuilder.MapRoute("Plugin.DiscountRules.PurchasedAllProducts.LoadProductFriendlyNames",
                 "Plugins/DiscountRulesPurchasedAllProducts/LoadProductFriendlyNames",
                 new { controller = "DiscountRulesPurchasedAllProducts", action = "LoadProductFriendlyNames" });
        }

        #endregion

        #region Properties

        public int Priority
        {
            get
            {
                return 0;
            }
        }

        #endregion
    }
}
