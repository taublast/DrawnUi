#pragma once

#include "PropertySetExtensions.g.h"

using namespace winrt::Windows::Foundation;
using namespace winrt::Windows::Foundation::Collections;

namespace winrt::SkiaSharp::DrawnUi::WinUI::Native::implementation
{
    struct PropertySetExtensions
    {
        PropertySetExtensions() = default;

        static void AddSingle(PropertySet const& propertySet, hstring const& key, float value);
        static void AddSize(PropertySet const& propertySet, hstring const& key, Size const& value);
    };
}

namespace winrt::SkiaSharp::DrawnUi::WinUI::Native::factory_implementation
{
    struct PropertySetExtensions : PropertySetExtensionsT<PropertySetExtensions, implementation::PropertySetExtensions>
    {
    };
}

// C-style exports for P/Invoke from C#
extern "C"
{
    __declspec(dllexport) void __stdcall AddSize(
        ::IUnknown* propertySet,
        const wchar_t* key,
        double width,
        double height);

    __declspec(dllexport) void __stdcall AddSingle(
        ::IUnknown* propertySet,
        const wchar_t* key,
        float value);
}
