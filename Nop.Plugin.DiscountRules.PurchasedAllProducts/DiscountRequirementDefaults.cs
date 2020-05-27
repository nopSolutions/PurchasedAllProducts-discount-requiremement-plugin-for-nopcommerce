namespace Nop.Plugin.DiscountRules.PurchasedAllProducts
{
    /// <summary>
    /// Represents constants for the discount requirement rule
    /// </summary>
    public static class DiscountRequirementDefaults
    {
        /// <summary>
        /// The system name of the discount requirement rule
        /// </summary>
        public const string SystemName = "DiscountRequirement.PurchasedAllProducts";

        /// <summary>
        /// The key of the settings to save restricted products
        /// </summary>
        public const string SettingsKey = "DiscountRequirement.RestrictedProductVariantIds-{0}";

        /// <summary>
        /// The HTML field prefix for discount requirements
        /// </summary>
        public const string HtmlFieldPrefix = "DiscountRulesPurchasedAllProducts{0}";
    }
}
