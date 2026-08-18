#pragma once

#include <string>

namespace aorf
{
    bool StartDailyLoginRoutingWorker();
    bool RunDailyLoginRoutingSelfTest();

    bool RewriteDailyLoginHttpRequestForTest(
        const std::string& input,
        std::string& output);
}
