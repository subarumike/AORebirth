#pragma once

#include <array>
#include <string>
#include <vector>

namespace aorf
{
    enum class DailyLoginMode { Off, PreserveHttp, RewriteHttp };

    struct LoginServerSelection
    {
        std::string address;
        std::wstring wideAddress;
        std::array<unsigned char, 4> addressBytes;
        std::string originalDailyHost;
        std::vector<std::string> dailyHosts;
        DailyLoginMode dailyMode;
        unsigned long port;
        bool matchedLaunchEndpoint;
    };

    bool TrySelectLoginServer(bool allowDailyDefault,
        LoginServerSelection& selection, const char*& failure);
    bool StartLoginKeyPatchWorker();
    bool RunLoginKeyPatchSelfTest(const wchar_t* packagedServers);
}
