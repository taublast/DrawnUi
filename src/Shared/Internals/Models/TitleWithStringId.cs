using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace DrawnUi.Internals
{
    public class TitleWithStringId : IHasTitleWithId
    {
        public string Id { get; set; }
        public string Title { get; set; }
    }
}
