using System.Collections;
using System.Collections.ObjectModel;

namespace DrawnUi.Infrastructure.Xaml
{

    public class SkiaShadowsCollection : ObservableCollection<SkiaShadow>, IList
    {

        public int Add(object value)
        {
            if (value is SkiaShadow skiaShadow)
            {
                base.Add(skiaShadow);
            }
            else
            {
                throw new InvalidOperationException("Invalid item type in Shadows collection");
            }
            return Count - 1;
        }


    }
}
