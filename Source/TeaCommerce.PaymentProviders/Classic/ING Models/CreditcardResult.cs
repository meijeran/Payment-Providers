using System.Collections.Generic;
using System;

namespace TeaCommerce.PaymentProviders.Classic
{
    internal class CreditcardResult
    {
        public int amount { get; set; }
        public ClientResult client { get; set; }
        public DateTime created { get; set; }
        public string currency { get; set; }
        public string description { get; set; }
        public List<string> flags { get; set; }
        public string id { get; set; }
        public DateTime last_transaction_added { get; set; }
        public string merchant_id { get; set; }
        public string merchant_order_id { get; set; }
        public DateTime modified { get; set; }
        public string project_id { get; set; }
        public string return_url { get; set; }
        public string status { get; set; }
        public List<TransactionResult> transactions { get; set; }
    }
}