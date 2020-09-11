using System;
using System.Collections.Generic;
using System.ComponentModel.DataAnnotations;
using System.Linq;
using System.Threading.Tasks;

namespace sap_azure_myo
{
    public class MyDataIntegration
    {
      
            [Required]
            [StringLength(200)]
            public string FromDate { get; set; }

            [Required]
            public string ToDate { get; set; }

        [Required]
        public String Option { get; set; }

            
    }
}
