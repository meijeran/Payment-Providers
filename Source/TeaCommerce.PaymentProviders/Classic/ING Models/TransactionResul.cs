using Newtonsoft.Json;

namespace TeaCommerce.PaymentProviders.Classic
{
    internal class TransactionResul
    {
        [JsonProperty("payment_method")]
        public string PaymentMethod { get; set; }
    }
}