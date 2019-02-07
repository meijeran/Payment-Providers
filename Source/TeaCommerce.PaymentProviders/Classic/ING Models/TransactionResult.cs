using System.Collections.Generic;
using System;

namespace TeaCommerce.PaymentProviders.Classic
{
    internal class TransactionResult
    {
        public int amount { get; set; }
        public string balance { get; set; }
        public DateTime created { get; set; }
        public string credit_debit { get; set; }
        public string currency { get; set; }
        public string description { get; set; }
        public List<EventResult> events { get; set; }
        public string expiration_period { get; set; }
        public string id { get; set; }
        public string merchant_id { get; set; }
        public DateTime modified { get; set; }
        public string order_id { get; set; }
        public string payment_method { get; set; }
        public PaymentMethodDetail payment_method_details { get; set; }
        public string payment_url { get; set; }
        public string product_type { get; set; }
        public string project_id { get; set; }
        public string status { get; set; }
    }
}