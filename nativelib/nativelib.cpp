#include <cstdlib>
#include <cstdio>
#include <cstring>

#ifdef _MSC_VER
#include <windows.h>
#define EXPORT __declspec(dllexport)

char* char_alloc_for_dotnet(size_t size)
{
    return static_cast<char*>(::CoTaskMemAlloc(size));
}

#else
#define EXPORT __attribute__((visibility("default")))

char* char_alloc_for_dotnet(size_t size)
{
    return static_cast<char*>(::malloc(size));
}

#endif

extern "C" EXPORT
char const* Greetings(char const* name)
{
    char const* greeting_format = "Hello, %s!";
    if (name == nullptr)
        name = "World";

    // This represents more space than is needed.
    size_t greeting_length = std::strlen(greeting_format) + std::strlen(name) + 1; // +1 for null terminator

    char* greeting = char_alloc_for_dotnet(greeting_length);
    if (greeting == nullptr)
        return nullptr; // Allocation failed

    std::snprintf(greeting, greeting_length, greeting_format, name);
    return greeting;
}