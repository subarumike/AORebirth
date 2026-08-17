#pragma once

#define AORF_WIDEN2(value) L##value
#define AORF_WIDEN(value) AORF_WIDEN2(value)

#ifndef AO_REBIRTH_CLIENT_PATCH_VERSION
#define AO_REBIRTH_CLIENT_PATCH_VERSION "2"
#endif

#ifndef AO_REBIRTH_CLIENT_PATCH_SOURCE_SHA
#define AO_REBIRTH_CLIENT_PATCH_SOURCE_SHA "unknown"
#endif

namespace aorf
{
    constexpr const char* ClientPatchVersion = AO_REBIRTH_CLIENT_PATCH_VERSION;
    constexpr const char* ClientPatchSourceSha = AO_REBIRTH_CLIENT_PATCH_SOURCE_SHA;
    constexpr const wchar_t* ClientPatchVersionW =
        AORF_WIDEN(AO_REBIRTH_CLIENT_PATCH_VERSION);
    constexpr const wchar_t* ClientPatchSourceShaW =
        AORF_WIDEN(AO_REBIRTH_CLIENT_PATCH_SOURCE_SHA);
}
