using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;

namespace TeaCommerce.PaymentProviders.Classic.ING_Models
{
    public class CurrencyConverterModel
    {
        public DateTime RateDate { get; set; }
        public string OriginalCurrency { get; set; }
        public decimal OriginalAmount { get; set; }
        public double ConversionRate { get; set; }
        public decimal ConvertedAmount { get; set; }

    }
}
