#include "pch.h"
#include "PropertySetExtensions.h"
#include "PropertySetExtensions.g.cpp"

using namespace winrt::Windows::Foundation;
using namespace winrt::Windows::Foundation::Collections;

namespace winrt::SkiaSharp::DrawnUi::WinUI::Native::implementation
{
    void PropertySetExtensions::AddSingle(PropertySet const& propertySet, hstring const& key, float value)
    {
        propertySet.Insert(key, PropertyValue::CreateSingle(value));
    }

    void PropertySetExtensions::AddSize(PropertySet const& propertySet, hstring const& key, Size const& height)
    {
        propertySet.Insert(key, PropertyValue::CreateSize(height));
    }
}

// C-style exports for P/Invoke from C#
extern "C"
{
    __declspec(dllexport) void __stdcall AddSize(
        ::IUnknown* propertySet,
        const wchar_t* key,
        double width,
        double height)
    {
        if (propertySet == nullptr || key == nullptr)
            return;

        try
        {
            // Convert IUnknown* to PropertySet
            PropertySet propSet = nullptr;
            winrt::copy_from_abi(propSet, propertySet);

            // Create Size and add to property set
            Size size{ static_cast<float>(width), static_cast<float>(height) };
            propSet.Insert(key, PropertyValue::CreateSize(size));
        }
        catch (...)
        {
            // Silently catch exceptions - P/Invoke doesn't propagate exceptions well
        }
    }

    __declspec(dllexport) void __stdcall AddSingle(
        ::IUnknown* propertySet,
        const wchar_t* key,
        float value)
    {
        if (propertySet == nullptr || key == nullptr)
            return;

        try
        {
            // Convert IUnknown* to PropertySet
            PropertySet propSet = nullptr;
            winrt::copy_from_abi(propSet, propertySet);

            // Add single value to property set
            propSet.Insert(key, PropertyValue::CreateSingle(value));
        }
        catch (...)
        {
            // Silently catch exceptions - P/Invoke doesn't propagate exceptions well
        }
    }
}
