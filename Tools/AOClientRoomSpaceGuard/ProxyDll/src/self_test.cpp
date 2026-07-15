#include "roomspace_fix.h"

#include <cstdio>

int main()
{
    if (!aorf::RunRoomSpaceFixSelfTest())
    {
        std::fprintf(stderr, "AORoomSpaceFix self-test failed.\n");
        return 1;
    }

    std::printf("AORoomSpaceFix self-test passed: profiles=2 callsites=5 wrapper=86.\n");
    return 0;
}
