using Newtonsoft.Json;

namespace TeaCommerce.PaymentProviders.Classic
{
    internal class PaymentMethodDetailsResult
    {
        [JsonProperty("issuer_id")]
        public string IssuerId { get; set; }
    }
}