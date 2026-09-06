// Targeted offline function evidence. Run against a private copied project.
// @category AORebirth.Evidence
import ghidra.app.script.GhidraScript;
import ghidra.app.decompiler.*;
import ghidra.program.model.address.*;
import ghidra.program.model.listing.*;
import ghidra.program.model.symbol.*;
import java.io.*;
import java.nio.charset.StandardCharsets;

public class ExportAcgEvidence extends GhidraScript {
    public void run() throws Exception {
        String[] args = getScriptArgs();
        DecompInterface decompiler = new DecompInterface();
        decompiler.openProgram(currentProgram);
        try (PrintWriter out = new PrintWriter(new OutputStreamWriter(new FileOutputStream(args[0]), StandardCharsets.UTF_8))) {
            out.println("Program: " + currentProgram.getName());
            out.println("Executable SHA256: " + currentProgram.getExecutableSHA256());
            out.println("Image base: " + currentProgram.getImageBase());
            for (int i = 1; i < args.length; i++) {
                Address address = currentProgram.getImageBase().add(Long.decode(args[i]));
                Function function = getFunctionContaining(address);
                if (function == null && currentProgram.getMemory().getBlock(address).isExecute()) {
                    disassemble(address);
                    function = createFunction(address, "EvidenceFunction_" + address);
                }
                out.println("\n## Target RVA " + args[i]);
                for (Reference ref : getReferencesTo(address)) {
                    out.println("XREF " + ref.getFromAddress() + " " + ref.getReferenceType() + " " + getFunctionContaining(ref.getFromAddress()));
                }
                if (function == null) { out.println("FUNCTION_UNAVAILABLE"); continue; }
                out.println("Function: " + function.getName(true) + " @ " + function.getEntryPoint());
                DecompileResults result = decompiler.decompileFunction(function, 45, monitor);
                if (result.decompileCompleted()) out.println(result.getDecompiledFunction().getC());
                else out.println("DECOMPILE_UNAVAILABLE " + result.getErrorMessage());
                out.println("ASSEMBLY:");
                InstructionIterator instructions = currentProgram.getListing().getInstructions(function.getBody(), true);
                while (instructions.hasNext()) {
                    Instruction instruction = instructions.next();
                    StringBuilder bytes = new StringBuilder();
                    for (byte b : instruction.getBytes()) bytes.append(String.format("%02x", b & 255));
                    out.println(instruction.getAddress() + " " + bytes + " " + instruction);
                }
            }
        } finally { decompiler.dispose(); }
    }
}
