﻿namespace Sandbox.Views
{
    public partial class MainPageCarouselTabs
    {


        public MainPageCarouselTabs()
        {
            try
            {
                InitializeComponent();

                //avoid setting context BEFORE InitializeComponent, can bug 
                //having parent BindingContext still null when constructing from xaml
                BindingContext = new MainPageViewModel();


            }
            catch (Exception e)
            {
                Super.DisplayException(this, e);
                Console.WriteLine(e);
            }
        }


        private void SkiaControl_OnTapped(object sender, SkiaControl.ControlTappedEventArgs e)
        {
     
        }
    }
}
