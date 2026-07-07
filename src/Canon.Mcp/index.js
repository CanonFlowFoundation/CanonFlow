import { Server } from "@modelcontextprotocol/sdk/server/index.js";
import { StdioServerTransport } from "@modelcontextprotocol/sdk/server/stdio.js";
import { CallToolRequestSchema, ListToolsRequestSchema } from "@modelcontextprotocol/sdk/types.js";
import { exec } from "child_process";
import * as fs from "fs";
import { fileURLToPath } from "url";
import { dirname } from "path";
import * as path from "path";
import { promisify } from "util";
const __filename = fileURLToPath(import.meta.url);
const __dirname = dirname(__filename);
const execAsync = promisify(exec);
const server = new Server({
    name: "canonflow-mcp",
    version: "1.0.0",
}, {
    capabilities: {
        tools: {},
    },
});
server.setRequestHandler(ListToolsRequestSchema, async () => {
    return {
        tools: [
            {
                name: "canonflow_introspect_schema",
                description: "Introspects a PostgreSQL database to extract strict mathematical bounds and constraints, emitting TypeScript validators and a mathematical proof of the schema.",
                inputSchema: {
                    type: "object",
                    properties: {
                        connectionString: {
                            type: "string",
                            description: "PostgreSQL connection string. Example: Host=localhost;Port=5432;Database=mydb;Username=user;Password=pass",
                        },
                    },
                    required: ["connectionString"],
                },
            },
        ],
    };
});
server.setRequestHandler(CallToolRequestSchema, async (request) => {
    if (request.params.name === "canonflow_introspect_schema") {
        const { connectionString } = request.params.arguments;
        try {
            // Run the CanonFlow CLI to extract constraints
            const cliPath = path.resolve(__dirname, "../../Canon.Cli/Canon.Cli.fsproj");
            const rootDir = path.resolve(__dirname, "../../../");
            const { stdout, stderr } = await execAsync(`dotnet run --project ${cliPath} -- --pg "${connectionString}" --contracts`, { cwd: rootDir });
            // Read the generated artifacts to feed back to the AI
            const validatorsPath = path.join(rootDir, "client/src/validators.ts");
            const proofPath = path.join(rootDir, "output/PROOF.md");
            let responseText = `CanonFlow Engine Output:\n${stdout}\n\n`;
            if (fs.existsSync(validatorsPath)) {
                responseText += `=== GENERATED TYPESCRIPT VALIDATORS ===\n${fs.readFileSync(validatorsPath, "utf-8")}\n\n`;
            }
            if (fs.existsSync(proofPath)) {
                responseText += `=== MATHEMATICAL PROOF ===\n${fs.readFileSync(proofPath, "utf-8")}\n`;
            }
            return {
                content: [
                    {
                        type: "text",
                        text: responseText,
                    },
                ],
            };
        }
        catch (error) {
            return {
                content: [
                    {
                        type: "text",
                        text: `Failed to run CanonFlow: ${error.message}\n${error.stdout}\n${error.stderr}`,
                    },
                ],
                isError: true,
            };
        }
    }
    throw new Error("Unknown tool");
});
async function main() {
    const transport = new StdioServerTransport();
    await server.connect(transport);
    console.error("CanonFlow Agentic Sidecar (MCP) running on stdio");
}
main().catch(console.error);
//# sourceMappingURL=index.js.map