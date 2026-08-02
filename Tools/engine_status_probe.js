// Read-only AORebirth engine process/listener ownership probe.
// Invoked through status-engines.cmd; requires no PowerShell or network access.

(function () {
    "use strict";

    var EXIT_HEALTH_MISMATCH = 1;
    var EXIT_PROBE_ERROR = 2;
    var EXIT_ALREADY_RUNNING = 3;
    var fileSystem = null;

    function getFileSystem() {
        if (fileSystem === null) {
            fileSystem = new ActiveXObject("Scripting.FileSystemObject");
        }

        return fileSystem;
    }

    function trim(value) {
        return String(value).replace(/^\s+|\s+$/g, "");
    }

    function normalizePath(value) {
        if (value === null || typeof value === "undefined") {
            return null;
        }

        var path = trim(value).replace(/^"+|"+$/g, "").replace(/\//g, "\\");
        if (path.length === 0) {
            return null;
        }

        try {
            path = getFileSystem().GetAbsolutePathName(path);
        }
        catch (ignored) {
        }

        while (path.length > 3 && path.charAt(path.length - 1) === "\\") {
            path = path.substring(0, path.length - 1);
        }

        return path.toLowerCase();
    }

    function canonicalEngineKey(value) {
        var key = trim(value).toLowerCase();
        if (key.length > 4 && key.substring(key.length - 4) === ".exe") {
            key = key.substring(0, key.length - 4);
        }

        if (key === "chatengine") {
            return "ChatEngine";
        }
        if (key === "loginengine") {
            return "LoginEngine";
        }
        if (key === "zoneengine") {
            return "ZoneEngine";
        }
        if (key === "webengine") {
            return "WebEngine";
        }

        throw new Error("Unknown engine name: " + value);
    }

    function parsePositiveInteger(value, label, maximum) {
        var text = trim(value);
        if (!/^\d+$/.test(text)) {
            throw new Error(label + " must be a positive integer.");
        }

        var number = parseInt(text, 10);
        if (number < 1 || number > maximum) {
            throw new Error(label + " is outside the supported range.");
        }

        return number;
    }

    function parseExpectedPid(options, specification, followingValue) {
        var separator = specification.indexOf("=");
        var engineName;
        var pidText;
        var consumedFollowing = false;

        if (separator >= 0) {
            engineName = specification.substring(0, separator);
            pidText = specification.substring(separator + 1);
        }
        else {
            if (followingValue === null) {
                throw new Error("--expect-pid requires Engine=PID or Engine PID.");
            }
            engineName = specification;
            pidText = followingValue;
            consumedFollowing = true;
        }

        var key = canonicalEngineKey(engineName);
        var pid = parsePositiveInteger(pidText, "Expected PID", 4294967295);
        if (typeof options.expectedPids[key] !== "undefined") {
            throw new Error("Expected PID was specified more than once for " + key + ".");
        }

        options.expectedPids[key] = pid;
        return consumedFollowing;
    }

    function parseArguments(args) {
        var options = {
            configPath: null,
            engineDirectory: null,
            mode: "core",
            modeWasExplicit: false,
            prestartEngine: null,
            requiredEngine: null,
            selfTest: false,
            expectedPids: {}
        };

        function selectMode(mode) {
            if (options.modeWasExplicit && options.mode !== mode) {
                throw new Error(
                    "Only one of --core, --web-required, --engine-required, or --prestart may be used.");
            }
            options.mode = mode;
            options.modeWasExplicit = true;
        }

        var index = 0;
        while (index < args.length) {
            var argument = String(args[index]);
            if (argument === "--config") {
                index++;
                if (index >= args.length) {
                    throw new Error("--config requires a path.");
                }
                options.configPath = String(args[index]);
            }
            else if (argument === "--engine-dir") {
                index++;
                if (index >= args.length) {
                    throw new Error("--engine-dir requires a path.");
                }
                options.engineDirectory = String(args[index]);
            }
            else if (argument === "--core") {
                selectMode("core");
            }
            else if (argument === "--web-required" || argument === "--web-only") {
                selectMode("web");
            }
            else if (argument === "--prestart") {
                selectMode("prestart");
                index++;
                if (index >= args.length) {
                    throw new Error("--prestart requires an engine name.");
                }
                options.prestartEngine = canonicalEngineKey(String(args[index]));
            }
            else if (argument === "--engine-required") {
                selectMode("engine");
                index++;
                if (index >= args.length) {
                    throw new Error("--engine-required requires an engine name.");
                }
                options.requiredEngine = canonicalEngineKey(String(args[index]));
            }
            else if (argument === "--expect-pid" || argument === "--expected-pid") {
                index++;
                if (index >= args.length) {
                    throw new Error("--expect-pid requires Engine=PID or Engine PID.");
                }

                var specification = String(args[index]);
                var following = index + 1 < args.length ? String(args[index + 1]) : null;
                if (parseExpectedPid(options, specification, following)) {
                    index++;
                }
            }
            else if (argument === "--self-test") {
                options.selfTest = true;
            }
            else {
                throw new Error("Unknown argument: " + argument);
            }

            index++;
        }

        if (options.mode === "prestart") {
            for (var expectedKey in options.expectedPids) {
                if (options.expectedPids.hasOwnProperty(expectedKey)) {
                    throw new Error("--prestart cannot be combined with --expect-pid.");
                }
            }
        }

        return options;
    }

    function loadConfigurationPorts(configPath) {
        var document = new ActiveXObject("Msxml2.DOMDocument.6.0");
        document.async = false;
        document.validateOnParse = false;
        document.resolveExternals = false;
        try {
            document.setProperty("ProhibitDTD", true);
            document.setProperty("SelectionLanguage", "XPath");
        }
        catch (ignored) {
        }

        if (!document.load(configPath)) {
            throw new Error("Repository configuration could not be loaded.");
        }

        function readPort(fieldName) {
            var nodes = document.selectNodes("/*/" + fieldName);
            if (nodes.length !== 1) {
                throw new Error("Configuration must contain exactly one " + fieldName + " value.");
            }

            return parsePositiveInteger(nodes.item(0).text, fieldName, 65535);
        }

        return {
            chat: readPort("ChatPort"),
            communication: readPort("CommPort"),
            login: readPort("LoginPort"),
            zone: readPort("ZonePort"),
            web: readPort("WebHostPort")
        };
    }

    function createDefinitions(engineDirectory, ports) {
        var root = getFileSystem().GetAbsolutePathName(engineDirectory);

        function definition(key, executable, enginePorts, required) {
            return {
                key: key,
                executable: executable,
                expectedPath: getFileSystem().BuildPath(root, executable),
                normalizedExpectedPath: normalizePath(getFileSystem().BuildPath(root, executable)),
                ports: enginePorts,
                required: required
            };
        }

        var definitions = [
            definition("ChatEngine", "ChatEngine.exe", [ports.communication, ports.chat], true),
            definition("LoginEngine", "LoginEngine.exe", [ports.login], true),
            definition("ZoneEngine", "ZoneEngine.exe", [ports.zone], true),
            definition("WebEngine", "WebEngine.exe", [ports.web], false)
        ];

        var claimedPorts = {};
        var definitionIndex;
        for (definitionIndex = 0; definitionIndex < definitions.length; definitionIndex++) {
            var portIndex;
            for (portIndex = 0; portIndex < definitions[definitionIndex].ports.length; portIndex++) {
                var port = definitions[definitionIndex].ports[portIndex];
                if (typeof claimedPorts[port] !== "undefined") {
                    throw new Error("Configured engine ports must be unique across engines.");
                }
                claimedPorts[port] = definitions[definitionIndex].key;
            }
        }

        return definitions;
    }

    function captureWindowsSnapshot() {
        var snapshot = { processes: [], listeners: [] };
        var locator;
        var processService;
        var tcpService;

        try {
            locator = new ActiveXObject("WbemScripting.SWbemLocator");
            processService = locator.ConnectServer(".", "root\\cimv2");
            var processRows = new Enumerator(
                processService.ExecQuery(
                    "SELECT ProcessId, Name, ExecutablePath FROM Win32_Process",
                    "WQL",
                    48));

            for (; !processRows.atEnd(); processRows.moveNext()) {
                var processRow = processRows.item();
                var executablePath = processRow.ExecutablePath;
                snapshot.processes.push({
                    pid: Number(processRow.ProcessId),
                    name: String(processRow.Name),
                    path: executablePath === null || typeof executablePath === "undefined"
                        ? null
                        : String(executablePath)
                });
            }
        }
        catch (processError) {
            throw new Error("Windows process ownership query failed.");
        }

        try {
            tcpService = locator.ConnectServer(".", "root\\StandardCimv2");
            var listenerRows = new Enumerator(
                tcpService.ExecQuery(
                    "SELECT LocalPort, OwningProcess FROM MSFT_NetTCPConnection WHERE State = 2",
                    "WQL",
                    48));

            for (; !listenerRows.atEnd(); listenerRows.moveNext()) {
                var listenerRow = listenerRows.item();
                snapshot.listeners.push({
                    port: Number(listenerRow.LocalPort),
                    pid: Number(listenerRow.OwningProcess)
                });
            }
        }
        catch (listenerError) {
            throw new Error("Windows TCP listener ownership query failed.");
        }

        return snapshot;
    }

    function addUnique(array, value) {
        var index;
        for (index = 0; index < array.length; index++) {
            if (array[index] === value) {
                return;
            }
        }
        array.push(value);
    }

    function joinNumbers(values) {
        if (values.length === 0) {
            return "none";
        }

        values.sort(function (left, right) { return left - right; });
        return values.join(",");
    }

    function addReason(reasons, reason) {
        var index;
        for (index = 0; index < reasons.length; index++) {
            if (reasons[index] === reason) {
                return;
            }
        }
        reasons.push(reason);
    }

    function findProcessByPid(processes, pid) {
        var index;
        for (index = 0; index < processes.length; index++) {
            if (processes[index].pid === pid) {
                return processes[index];
            }
        }
        return null;
    }

    function processesNamed(processes, executable) {
        var matches = [];
        var expectedName = executable.toLowerCase();
        var index;
        for (index = 0; index < processes.length; index++) {
            if (String(processes[index].name).toLowerCase() === expectedName) {
                matches.push(processes[index]);
            }
        }
        return matches;
    }

    function processesAtExpectedPath(processes, expectedPath) {
        var matches = [];
        var index;
        for (index = 0; index < processes.length; index++) {
            var processPath = normalizePath(processes[index].path);
            if (processPath !== null && processPath === expectedPath) {
                matches.push(processes[index]);
            }
        }
        return matches;
    }

    function listenerPidsForPort(listeners, port) {
        var pids = [];
        var index;
        for (index = 0; index < listeners.length; index++) {
            if (listeners[index].port === port) {
                addUnique(pids, listeners[index].pid);
            }
        }
        return pids;
    }

    function hasExpectedPid(options, key) {
        return typeof options.expectedPids[key] !== "undefined";
    }

    function selectedDefinitions(definitions, options) {
        var selected = [];
        var index;
        for (index = 0; index < definitions.length; index++) {
            if (options.mode === "core"
                || (options.mode === "web" && definitions[index].key === "WebEngine")
                || (options.mode === "engine" && definitions[index].key === options.requiredEngine)
                || (options.mode === "prestart" && definitions[index].key === options.prestartEngine)) {
                selected.push(definitions[index]);
            }
        }

        if (selected.length === 0) {
            throw new Error("The selected engine mode did not resolve an engine definition.");
        }

        for (var key in options.expectedPids) {
            if (options.expectedPids.hasOwnProperty(key)) {
                var found = false;
                for (index = 0; index < selected.length; index++) {
                    if (selected[index].key === key) {
                        found = true;
                        break;
                    }
                }
                if (!found) {
                    throw new Error("Expected PID was supplied for an engine excluded by the selected mode.");
                }
            }
        }

        return selected;
    }

    function evaluate(definitions, snapshot, options) {
        var selected = selectedDefinitions(definitions, options);
        var result = { ok: true, lines: [] };
        var definitionIndex;

        for (definitionIndex = 0; definitionIndex < selected.length; definitionIndex++) {
            var definition = selected[definitionIndex];
            var required = (options.mode === "core" && definition.required)
                || options.mode === "web"
                || options.mode === "engine"
                || options.mode === "prestart"
                || hasExpectedPid(options, definition.key);
            var named = processesNamed(snapshot.processes, definition.executable);
            var exact = processesAtExpectedPath(named, definition.normalizedExpectedPath);
            var namedPids = [];
            var namedIndex;
            for (namedIndex = 0; namedIndex < named.length; namedIndex++) {
                addUnique(namedPids, named[namedIndex].pid);
            }

            var portRecords = [];
            var allPortsClosed = true;
            var portIndex;
            for (portIndex = 0; portIndex < definition.ports.length; portIndex++) {
                var listenerPids = listenerPidsForPort(snapshot.listeners, definition.ports[portIndex]);
                if (listenerPids.length > 0) {
                    allPortsClosed = false;
                }
                portRecords.push({
                    port: definition.ports[portIndex],
                    listenerPids: listenerPids,
                    listenerProcess: "none",
                    reasons: []
                });
            }

            if (!required && named.length === 0 && allPortsClosed) {
                for (portIndex = 0; portIndex < portRecords.length; portIndex++) {
                    result.lines.push({
                        definition: definition,
                        processPids: namedPids,
                        port: portRecords[portIndex].port,
                        listenerPids: portRecords[portIndex].listenerPids,
                        listenerProcess: "none",
                        ownership: "INACTIVE",
                        reasons: ["optional-engine-absent"]
                    });
                }
                continue;
            }

            var engineReasons = [];
            if (named.length === 0) {
                addReason(engineReasons, "expected-process-absent");
            }
            else if (exact.length === 0) {
                var unavailablePath = false;
                for (namedIndex = 0; namedIndex < named.length; namedIndex++) {
                    if (normalizePath(named[namedIndex].path) === null) {
                        unavailablePath = true;
                    }
                }
                addReason(
                    engineReasons,
                    unavailablePath ? "process-path-unavailable" : "unexpected-executable-path");
            }
            else if (exact.length > 1) {
                addReason(engineReasons, "multiple-expected-processes");
            }

            var expectedPid = hasExpectedPid(options, definition.key)
                ? options.expectedPids[definition.key]
                : null;
            if (expectedPid !== null) {
                var requestedProcess = findProcessByPid(snapshot.processes, expectedPid);
                if (requestedProcess === null) {
                    addReason(engineReasons, "expected-pid-absent");
                }
                else if (String(requestedProcess.name).toLowerCase() !== definition.executable.toLowerCase()
                    || normalizePath(requestedProcess.path) !== definition.normalizedExpectedPath) {
                    addReason(engineReasons, "expected-pid-executable-mismatch");
                }
            }

            var singleExactPid = exact.length === 1 ? exact[0].pid : null;
            if (expectedPid !== null && singleExactPid !== null && singleExactPid !== expectedPid) {
                addReason(engineReasons, "expected-pid-mismatch");
            }

            var singleListenerOwners = [];
            for (portIndex = 0; portIndex < portRecords.length; portIndex++) {
                var record = portRecords[portIndex];
                if (record.listenerPids.length === 0) {
                    addReason(record.reasons, "port-closed");
                    continue;
                }
                if (record.listenerPids.length > 1) {
                    addReason(record.reasons, "conflicting-listeners");
                    continue;
                }

                var listenerPid = record.listenerPids[0];
                addUnique(singleListenerOwners, listenerPid);
                var owner = findProcessByPid(snapshot.processes, listenerPid);
                if (owner === null) {
                    record.listenerProcess = "unknown";
                    addReason(record.reasons, "listener-pid-unresolved");
                    continue;
                }

                record.listenerProcess = String(owner.name);
                var ownerPath = normalizePath(owner.path);
                if (ownerPath === null) {
                    addReason(record.reasons, "listener-path-unavailable");
                }
                else if (String(owner.name).toLowerCase() !== definition.executable.toLowerCase()) {
                    addReason(record.reasons, "wrong-process-owner");
                }
                else if (ownerPath !== definition.normalizedExpectedPath) {
                    addReason(record.reasons, "wrong-executable-owner");
                }

                if (singleExactPid !== null && listenerPid !== singleExactPid) {
                    addReason(record.reasons, "wrong-process-instance");
                }
                if (expectedPid !== null && listenerPid !== expectedPid) {
                    addReason(record.reasons, "expected-pid-mismatch");
                }
            }

            if (singleListenerOwners.length > 1) {
                addReason(engineReasons, "split-port-ownership");
            }

            var engineOk = engineReasons.length === 0;
            for (portIndex = 0; portIndex < portRecords.length; portIndex++) {
                if (portRecords[portIndex].reasons.length > 0) {
                    engineOk = false;
                }
            }

            if (!engineOk) {
                result.ok = false;
            }

            for (portIndex = 0; portIndex < portRecords.length; portIndex++) {
                var combinedReasons = [];
                var reasonIndex;
                for (reasonIndex = 0; reasonIndex < engineReasons.length; reasonIndex++) {
                    addReason(combinedReasons, engineReasons[reasonIndex]);
                }
                for (reasonIndex = 0; reasonIndex < portRecords[portIndex].reasons.length; reasonIndex++) {
                    addReason(combinedReasons, portRecords[portIndex].reasons[reasonIndex]);
                }

                result.lines.push({
                    definition: definition,
                    processPids: namedPids,
                    port: portRecords[portIndex].port,
                    listenerPids: portRecords[portIndex].listenerPids,
                    listenerProcess: portRecords[portIndex].listenerProcess,
                    ownership: combinedReasons.length === 0 ? "PASS" : "FAIL",
                    reasons: combinedReasons.length === 0 ? ["verified"] : combinedReasons
                });
            }
        }

        return result;
    }

    function printResult(result) {
        var index;
        for (index = 0; index < result.lines.length; index++) {
            var line = result.lines[index];
            WScript.Echo(
                "[AORebirth Status] engine=" + line.definition.key
                + " expectedExecutable=\"" + line.definition.expectedPath + "\""
                + " processPid=" + joinNumbers(line.processPids)
                + " port=" + line.port
                + " listenerPid=" + joinNumbers(line.listenerPids)
                + " listenerProcess=" + line.listenerProcess
                + " ownership=" + line.ownership
                + " reason=" + line.reasons.join("+")
            );
        }
    }

    function evaluatePrestart(definitions, snapshot, options) {
        var selected = selectedDefinitions(definitions, options);
        var definition = selected[0];
        var named = processesNamed(snapshot.processes, definition.executable);
        var allPortsClosed = true;
        var portIndex;
        for (portIndex = 0; portIndex < definition.ports.length; portIndex++) {
            if (listenerPidsForPort(snapshot.listeners, definition.ports[portIndex]).length > 0) {
                allPortsClosed = false;
            }
        }

        if (named.length === 0 && allPortsClosed) {
            var clearResult = { ok: true, lines: [] };
            for (portIndex = 0; portIndex < definition.ports.length; portIndex++) {
                clearResult.lines.push({
                    definition: definition,
                    processPids: [],
                    port: definition.ports[portIndex],
                    listenerPids: [],
                    listenerProcess: "none",
                    ownership: "CLEAR",
                    reasons: ["prestart-clear"]
                });
            }
            return { exitCode: 0, result: clearResult, state: "CLEAR" };
        }

        var ownershipResult = evaluate(definitions, snapshot, options);
        if (ownershipResult.ok) {
            return {
                exitCode: EXIT_ALREADY_RUNNING,
                result: ownershipResult,
                state: "ALREADY_RUNNING"
            };
        }

        return {
            exitCode: EXIT_HEALTH_MISMATCH,
            result: ownershipResult,
            state: "MISMATCH"
        };
    }

    function testDefinitions() {
        return createDefinitions(
            "C:\\AORebirth\\AORebirth\\Built\\Debug",
            { communication: 6996, chat: 7012, login: 7500, zone: 7501, web: 8181 });
    }

    function healthySnapshot(includeWeb) {
        var base = "C:\\AORebirth\\AORebirth\\Built\\Debug\\";
        var snapshot = {
            processes: [
                { pid: 101, name: "ChatEngine.exe", path: base + "ChatEngine.exe" },
                { pid: 201, name: "LoginEngine.exe", path: base + "LoginEngine.exe" },
                { pid: 301, name: "ZoneEngine.exe", path: base + "ZoneEngine.exe" }
            ],
            listeners: [
                { port: 6996, pid: 101 },
                { port: 7012, pid: 101 },
                { port: 7500, pid: 201 },
                { port: 7501, pid: 301 }
            ]
        };

        if (includeWeb) {
            snapshot.processes.push({ pid: 401, name: "WebEngine.exe", path: base + "WebEngine.exe" });
            snapshot.listeners.push({ port: 8181, pid: 401 });
        }

        return snapshot;
    }

    function defaultTestOptions() {
        return {
            configPath: null,
            engineDirectory: null,
            mode: "core",
            modeWasExplicit: false,
            prestartEngine: null,
            requiredEngine: null,
            selfTest: true,
            expectedPids: {}
        };
    }

    function resultContains(result, token) {
        var lineIndex;
        for (lineIndex = 0; lineIndex < result.lines.length; lineIndex++) {
            var reasonIndex;
            for (reasonIndex = 0; reasonIndex < result.lines[lineIndex].reasons.length; reasonIndex++) {
                if (result.lines[lineIndex].reasons[reasonIndex] === token) {
                    return true;
                }
            }
        }
        return false;
    }

    function runSelfTests() {
        var definitions = testDefinitions();
        var passed = 0;
        var total = 0;

        function verify(name, expectedOk, snapshot, options, expectedReason) {
            total++;
            var result = evaluate(definitions, snapshot, options);
            if (result.ok !== expectedOk) {
                throw new Error("Self-test " + name + " returned the wrong health result.");
            }
            if (expectedReason !== null && !resultContains(result, expectedReason)) {
                throw new Error("Self-test " + name + " did not report " + expectedReason + ".");
            }
            passed++;
            WScript.Echo("[AORebirth Status Test] PASS case=" + name);
        }

        verify(
            "all-engines-absent",
            false,
            { processes: [], listeners: [] },
            defaultTestOptions(),
            "expected-process-absent");

        verify(
            "correct-ownership-optional-web-absent",
            true,
            healthySnapshot(false),
            defaultTestOptions(),
            "optional-engine-absent");

        verify(
            "multiple-ports-same-correct-engine",
            true,
            healthySnapshot(false),
            defaultTestOptions(),
            "verified");

        var wrongOwner = healthySnapshot(false);
        wrongOwner.processes.push({ pid: 901, name: "WrongProcess.exe", path: "C:\\Other\\WrongProcess.exe" });
        wrongOwner.listeners[0].pid = 901;
        verify(
            "wrong-process-owning-port",
            false,
            wrongOwner,
            defaultTestOptions(),
            "wrong-process-owner");

        var missingPort = healthySnapshot(false);
        missingPort.listeners.splice(2, 1);
        verify(
            "process-present-port-absent",
            false,
            missingPort,
            defaultTestOptions(),
            "port-closed");

        var processAbsent = healthySnapshot(false);
        processAbsent.processes.splice(2, 1);
        processAbsent.processes.push({ pid: 902, name: "Other.exe", path: "C:\\Other\\Other.exe" });
        processAbsent.listeners[3].pid = 902;
        verify(
            "port-present-expected-process-absent",
            false,
            processAbsent,
            defaultTestOptions(),
            "expected-process-absent");

        var conflicting = healthySnapshot(false);
        conflicting.processes.push({ pid: 903, name: "Other.exe", path: "C:\\Other\\Other.exe" });
        conflicting.listeners.push({ port: 7500, pid: 903 });
        verify(
            "multiple-conflicting-listeners",
            false,
            conflicting,
            defaultTestOptions(),
            "conflicting-listeners");

        var duplicateSamePid = healthySnapshot(false);
        duplicateSamePid.listeners.push({ port: 6996, pid: 101 });
        verify(
            "same-pid-dual-stack-listeners",
            true,
            duplicateSamePid,
            defaultTestOptions(),
            "verified");

        var splitOwnership = healthySnapshot(false);
        splitOwnership.processes.push({
            pid: 102,
            name: "ChatEngine.exe",
            path: "C:\\AORebirth\\AORebirth\\Built\\Debug\\ChatEngine.exe"
        });
        splitOwnership.listeners[1].pid = 102;
        verify(
            "split-multiport-engine-ownership",
            false,
            splitOwnership,
            defaultTestOptions(),
            "split-port-ownership");

        var unavailableOwnership = healthySnapshot(false);
        unavailableOwnership.processes[0].path = null;
        verify(
            "pid-ownership-cannot-be-established",
            false,
            unavailableOwnership,
            defaultTestOptions(),
            "listener-path-unavailable");

        var requiredWebOptions = defaultTestOptions();
        requiredWebOptions.mode = "web";
        verify(
            "web-required-but-absent",
            false,
            healthySnapshot(false),
            requiredWebOptions,
            "expected-process-absent");

        var webRequiredWithoutCore = {
            processes: [{
                pid: 401,
                name: "WebEngine.exe",
                path: "C:\\AORebirth\\AORebirth\\Built\\Debug\\WebEngine.exe"
            }],
            listeners: [{ port: 8181, pid: 401 }]
        };
        verify(
            "web-required-ignores-core-engines",
            true,
            webRequiredWithoutCore,
            requiredWebOptions,
            "verified");

        var webOnlyExpectedOptions = defaultTestOptions();
        webOnlyExpectedOptions.mode = "web";
        webOnlyExpectedOptions.expectedPids.WebEngine = 401;
        verify(
            "web-only-exact-expected-pid",
            true,
            healthySnapshot(true),
            webOnlyExpectedOptions,
            "verified");

        var wrongExpectedOptions = defaultTestOptions();
        wrongExpectedOptions.mode = "web";
        wrongExpectedOptions.expectedPids.WebEngine = 499;
        verify(
            "web-only-wrong-expected-pid",
            false,
            healthySnapshot(true),
            wrongExpectedOptions,
            "expected-pid-absent");

        var optionalWebConflict = healthySnapshot(false);
        optionalWebConflict.processes.push({ pid: 904, name: "Other.exe", path: "C:\\Other\\Other.exe" });
        optionalWebConflict.listeners.push({ port: 8181, pid: 904 });
        verify(
            "optional-web-port-conflict",
            false,
            optionalWebConflict,
            defaultTestOptions(),
            "wrong-process-owner");

        var parsed = parseArguments(["--web-required", "--expect-pid", "WebEngine=401"]);
        if (parsed.mode !== "web" || parsed.expectedPids.WebEngine !== 401) {
            throw new Error("Self-test command-line-mode-parsing returned the wrong result.");
        }
        total++;
        passed++;
        WScript.Echo("[AORebirth Status Test] PASS case=command-line-mode-parsing");

        var engineRequiredOptions = defaultTestOptions();
        engineRequiredOptions.mode = "engine";
        engineRequiredOptions.requiredEngine = "ChatEngine";
        engineRequiredOptions.expectedPids.ChatEngine = 101;
        var chatOnlySnapshot = {
            processes: [{
                pid: 101,
                name: "ChatEngine.exe",
                path: "C:\\AORebirth\\AORebirth\\Built\\Debug\\ChatEngine.exe"
            }],
            listeners: [
                { port: 6996, pid: 101 },
                { port: 7012, pid: 101 }
            ]
        };
        verify(
            "single-engine-required-exact-pid",
            true,
            chatOnlySnapshot,
            engineRequiredOptions,
            "verified");

        var parsedEngine = parseArguments([
            "--engine-required",
            "ChatEngine",
            "--expect-pid",
            "ChatEngine",
            "101"
        ]);
        if (parsedEngine.mode !== "engine"
            || parsedEngine.requiredEngine !== "ChatEngine"
            || parsedEngine.expectedPids.ChatEngine !== 101) {
            throw new Error("Self-test single-engine-mode-parsing returned the wrong result.");
        }
        total++;
        passed++;
        WScript.Echo("[AORebirth Status Test] PASS case=single-engine-mode-parsing");

        var parsedRepeatedPids = parseArguments([
            "--core",
            "--expect-pid",
            "ChatEngine=101",
            "--expect-pid",
            "LoginEngine=201",
            "--expect-pid",
            "ZoneEngine=301"
        ]);
        if (parsedRepeatedPids.mode !== "core"
            || parsedRepeatedPids.expectedPids.ChatEngine !== 101
            || parsedRepeatedPids.expectedPids.LoginEngine !== 201
            || parsedRepeatedPids.expectedPids.ZoneEngine !== 301) {
            throw new Error("Self-test repeated-expected-pid-parsing returned the wrong result.");
        }
        total++;
        passed++;
        WScript.Echo("[AORebirth Status Test] PASS case=repeated-expected-pid-parsing");

        var prestartClearOptions = defaultTestOptions();
        prestartClearOptions.mode = "prestart";
        prestartClearOptions.prestartEngine = "WebEngine";
        var prestartClear = evaluatePrestart(definitions, healthySnapshot(false), prestartClearOptions);
        if (prestartClear.exitCode !== 0 || prestartClear.state !== "CLEAR") {
            throw new Error("Self-test prestart-clear returned the wrong result.");
        }
        total++;
        passed++;
        WScript.Echo("[AORebirth Status Test] PASS case=prestart-clear");

        var prestartRunning = evaluatePrestart(definitions, healthySnapshot(true), prestartClearOptions);
        if (prestartRunning.exitCode !== EXIT_ALREADY_RUNNING
            || prestartRunning.state !== "ALREADY_RUNNING") {
            throw new Error("Self-test prestart-already-running returned the wrong result.");
        }
        total++;
        passed++;
        WScript.Echo("[AORebirth Status Test] PASS case=prestart-already-running");

        var prestartConflictSnapshot = healthySnapshot(false);
        prestartConflictSnapshot.processes.push({
            pid: 905,
            name: "Other.exe",
            path: "C:\\Other\\Other.exe"
        });
        prestartConflictSnapshot.listeners.push({ port: 8181, pid: 905 });
        var prestartConflict = evaluatePrestart(
            definitions,
            prestartConflictSnapshot,
            prestartClearOptions);
        if (prestartConflict.exitCode !== EXIT_HEALTH_MISMATCH
            || prestartConflict.state !== "MISMATCH") {
            throw new Error("Self-test prestart-conflict returned the wrong result.");
        }
        total++;
        passed++;
        WScript.Echo("[AORebirth Status Test] PASS case=prestart-conflict");

        WScript.Echo("[AORebirth Status Test] PASS - " + passed + "/" + total + " deterministic cases.");
    }

    function main() {
        var args = [];
        var index;
        for (index = 0; index < WScript.Arguments.length; index++) {
            args.push(String(WScript.Arguments.Item(index)));
        }

        var options = parseArguments(args);
        if (options.selfTest) {
            runSelfTests();
            return 0;
        }

        if (options.configPath === null || options.engineDirectory === null) {
            throw new Error("--config and --engine-dir are required.");
        }

        var ports = loadConfigurationPorts(options.configPath);
        var definitions = createDefinitions(options.engineDirectory, ports);
        var snapshot = captureWindowsSnapshot();

        if (options.mode === "prestart") {
            var prestart = evaluatePrestart(definitions, snapshot, options);
            printResult(prestart.result);
            if (prestart.state === "CLEAR") {
                WScript.Echo("[AORebirth Status] PASS - selected engine is absent and all expected ports are closed.");
            }
            else if (prestart.state === "ALREADY_RUNNING") {
                WScript.Echo("[AORebirth Status] ALREADY_RUNNING - selected engine already owns every expected port.");
            }
            else {
                WScript.Echo("[AORebirth Status] FAIL - selected engine has a partial or conflicting prestart state.");
            }
            return prestart.exitCode;
        }

        var result = evaluate(definitions, snapshot, options);
        printResult(result);

        if (!result.ok) {
            WScript.Echo("[AORebirth Status] FAIL - one or more engine process/listener ownership checks failed.");
            return EXIT_HEALTH_MISMATCH;
        }

        WScript.Echo("[AORebirth Status] PASS - selected engine process/listener ownership is verified.");
        return 0;
    }

    try {
        WScript.Quit(main());
    }
    catch (error) {
        var message = error && error.message ? String(error.message) : "unknown status probe error";
        message = message.replace(/[\r\n]+/g, " ");
        WScript.Echo("[AORebirth Status] FAIL - " + message);
        WScript.Quit(EXIT_PROBE_ERROR);
    }
}());
