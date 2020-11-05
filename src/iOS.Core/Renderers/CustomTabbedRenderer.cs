﻿using Bit.iOS.Core.Renderers;
using Xamarin.Forms;
using Xamarin.Forms.Platform.iOS;

[assembly: ExportRenderer(typeof(TabbedPage), typeof(CustomTabbedRenderer))]
namespace Bit.iOS.Core.Renderers
{
    public class CustomTabbedRenderer : TabbedRenderer
    {
        protected override void OnElementChanged(VisualElementChangedEventArgs e)
        {
            base.OnElementChanged(e);
            TabBar.Translucent = false;
            TabBar.Opaque = true;
        }
    }
}
