#pragma once

namespace aorf
{
    // Gamecode's playfield-name lookup only knows ~100 hard-coded playfields; the LFT window shows "Not found" for
    // every other one. Falls back to the names from the client's own playfield records.
    bool InstallPlayfieldNameFix();
}
