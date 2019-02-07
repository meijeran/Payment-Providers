using System.Collections.Generic;
using Newtonsoft.Json;

namespace TeaCommerce.PaymentProviders.Classic
{
    internal class IdealPayment : Payment
    {     
        [JsonProperty("issuer_id")]
        public string IssuerId { get; set; }

        public override List<TransactionResul> PaymentType => new List<TransactionResul> { new IdealTransaction() };
    }
}