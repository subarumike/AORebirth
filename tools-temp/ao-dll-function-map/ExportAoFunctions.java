// Export a per-program function table from Ghidra headless analysis.
// Args:
//   0: output directory

import ghidra.app.script.GhidraScript;
import ghidra.program.model.listing.Function;
import ghidra.program.model.listing.FunctionIterator;
import ghidra.program.model.symbol.Namespace;
import ghidra.program.model.symbol.Symbol;

import java.io.File;
import java.io.OutputStreamWriter;
import java.io.FileOutputStream;
import java.io.PrintWriter;
import java.nio.charset.StandardCharsets;

public class ExportAoFunctions extends GhidraScript {
    @Override
    protected void run() throws Exception {
        String[] args = getScriptArgs();
        if (args.length < 1) {
            throw new IllegalArgumentException("Missing output directory argument.");
        }

        File outDir = new File(args[0]);
        if (!outDir.exists() && !outDir.mkdirs()) {
            throw new IllegalStateException("Could not create output directory: " + outDir);
        }

        String programName = currentProgram.getName();
        File outFile = new File(outDir, sanitize(programName) + ".ghidra_functions.csv");

        try (PrintWriter writer = new PrintWriter(new OutputStreamWriter(new FileOutputStream(outFile), StandardCharsets.UTF_8))) {
            writer.println("Program,ExecutablePath,EntryPoint,Name,Namespace,Signature,CallingConvention,SourceType,IsExternal,IsThunk,ThunkTarget,BodyAddressCount,ReadableName");

            FunctionIterator functions = currentProgram.getListing().getFunctions(true);
            while (functions.hasNext() && !monitor.isCancelled()) {
                Function function = functions.next();
                Symbol symbol = function.getSymbol();
                Namespace namespace = function.getParentNamespace();
                Function thunked = null;
                try {
                    thunked = function.getThunkedFunction(true);
                }
                catch (Exception ignored) {
                }

                String name = function.getName();
                String namespaceName = namespace == null ? "" : namespace.getName(true);
                String signature = safeSignature(function);
                String callingConvention = safeString(function.getCallingConventionName());
                String sourceType = symbol == null ? "" : symbol.getSource().toString();
                boolean isExternal = function.isExternal();
                boolean isThunk = function.isThunk();
                String thunkTarget = thunked == null ? "" : thunked.getName(true);
                long bodyCount = function.getBody() == null ? 0 : function.getBody().getNumAddresses();
                boolean readableName = isReadableFunctionName(name, sourceType);

                writer.println(
                    csv(programName) + "," +
                    csv(safeString(currentProgram.getExecutablePath())) + "," +
                    csv(function.getEntryPoint().toString()) + "," +
                    csv(name) + "," +
                    csv(namespaceName) + "," +
                    csv(signature) + "," +
                    csv(callingConvention) + "," +
                    csv(sourceType) + "," +
                    csv(Boolean.toString(isExternal)) + "," +
                    csv(Boolean.toString(isThunk)) + "," +
                    csv(thunkTarget) + "," +
                    csv(Long.toString(bodyCount)) + "," +
                    csv(Boolean.toString(readableName))
                );
            }
        }
    }

    private static String safeSignature(Function function) {
        try {
            return function.getSignature(true).toString();
        }
        catch (Exception ex) {
            return "";
        }
    }

    private static boolean isReadableFunctionName(String name, String sourceType) {
        if (name == null || name.length() == 0) {
            return false;
        }

        if (name.startsWith("FUN_") ||
            name.startsWith("SUB_") ||
            name.startsWith("LAB_") ||
            name.startsWith("switchD_") ||
            name.startsWith("thunk_FUN_")) {
            return false;
        }

        return !"DEFAULT".equals(sourceType);
    }

    private static String safeString(String value) {
        return value == null ? "" : value;
    }

    private static String sanitize(String value) {
        return value.replaceAll("[^A-Za-z0-9_.-]", "_");
    }

    private static String csv(String value) {
        if (value == null) {
            value = "";
        }

        return "\"" + value.replace("\"", "\"\"") + "\"";
    }
}
