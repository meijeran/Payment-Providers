using Newtonsoft.Json;

namespace TeaCommerce.PaymentProviders.Classic
{
    internal class IdealTransaction : TransactionResul
    {
        [JsonProperty("payment_method_details")]
        public PaymentMethodDetailsResult PaymentMethodDetails { get; set; }
    }
}