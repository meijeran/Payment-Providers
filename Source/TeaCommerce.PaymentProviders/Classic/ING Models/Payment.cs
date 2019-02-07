using System.Collections.Generic;
using Newtonsoft.Json;

namespace TeaCommerce.PaymentProviders.Classic
{
    internal abstract class Payment
    {
        [JsonProperty("merchant_order_id")]
        public string OrderId { get; set; }

        [JsonProperty("amount")]
        public int TotalAmount { get; set; }

        [JsonProperty("currency")]
        public string Currency => "EUR";

        [JsonProperty("description")]
        public string Description { get; set; }

        [JsonProperty("return_url")]
        public string ReturnUrl { get; set; }

        [JsonProperty("transactions")]
        public abstract List<TransactionResul> PaymentType { get; }

    }
}