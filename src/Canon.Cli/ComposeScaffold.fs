namespace Canon.Cli

open Canon.Introspect

module ComposeScaffold =
    
    let generateForm (table: TableDef) : string =
        let tableName = table.Name
        let componentName = $"{tableName.Substring(0, 1).ToUpper()}{tableName.Substring(1)}Form"
        
        let imports = 
            [ "import androidx.compose.foundation.layout.*"
              "import androidx.compose.material3.*"
              "import androidx.compose.runtime.*"
              "import androidx.compose.ui.Modifier"
              "import androidx.compose.ui.unit.dp"
              "import com.layam.validators.*" ]
            |> String.concat "\n"

        let inputs = 
            table.Columns
            |> List.map (fun c -> 
                let inputType = 
                    match c.DataType.ToLower() with
                    | "integer" | "numeric" | "decimal" -> "androidx.compose.ui.text.input.KeyboardType.Number"
                    | _ -> "androidx.compose.ui.text.input.KeyboardType.Text"
                
                let validationCall =
                    if c.CheckConstraints.IsEmpty then
                        "true"
                    else
                        $"validate_{tableName}_{c.Name}({c.Name}Value)"

                $"""        var {c.Name}Value by remember {{ mutableStateOf("") }} // FsAssay-Ignore
        val is{c.Name}Valid = {validationCall}
        OutlinedTextField(
            value = {c.Name}Value,
            onValueChange = {{ {c.Name}Value = it }},
            label = {{ Text("{c.Name}") }},
            isError = !is{c.Name}Valid,
            keyboardOptions = androidx.compose.foundation.text.KeyboardOptions(keyboardType = {inputType}),
            modifier = Modifier.fillMaxWidth().padding(bottom = 16.dp)
        )
        if (!is{c.Name}Valid) {{
            Text("Invalid {c.Name}", color = MaterialTheme.colorScheme.error, style = MaterialTheme.typography.bodySmall)
        }}"""
            )
            |> String.concat "\n\n"

        $"""{imports}

@Composable
fun {componentName}(onSubmit: (Map<String, String>) -> Unit) {{
    Column(modifier = Modifier.padding(16.dp)) {{
        Text("{componentName}", style = MaterialTheme.typography.headlineSmall, modifier = Modifier.padding(bottom = 24.dp))
        
{inputs}

        Button(
            onClick = {{ onSubmit(emptyMap()) /* TODO: map state to data class */ }},
            modifier = Modifier.fillMaxWidth().padding(top = 16.dp)
        ) {{
            Text("Submit")
        }}
    }}
}}
"""
    let tryGenerateSmartComposeAsync (table: TableDef) : Async<string option> = async {
        let apiKey = System.Environment.GetEnvironmentVariable("OPENAI_API_KEY")
        if System.String.IsNullOrWhiteSpace(apiKey) then
            return None
        else
            let tableName = table.Name
            let schemaJson = 
                table.Columns 
                |> List.map (fun c -> $"- {c.Name} ({c.DataType}): constraints: %A{c.CheckConstraints}")
                |> String.concat "\n"
            
            let prompt = $"""
You are an expert Android Jetpack Compose developer. 
I need a beautiful, highly styled, modern Jetpack Compose UI component for a PostgreSQL table named "{tableName}".
Here is the schema and constraints for the table:
{schemaJson}

Requirements:
1. Output ONLY valid Kotlin code for the Compose function, no markdown wrappers, no explanations.
2. Use modern Material 3 design (Cards, beautiful typography, proper padding, primary colors, OutlinedTextFields).
3. The component MUST import its mathematical validators via `import com.layam.validators.*`
4. The form must validate fields like `val isValid = validate_tableName_columnName(value)` if constraints exist, showing error states.
5. Create a stunning user experience that feels state-of-the-art.
"""
            try
                use client = new System.Net.Http.HttpClient()
                client.DefaultRequestHeaders.Add("Authorization", $"Bearer {apiKey}")
                
                let reqBody = 
                    {| model = "gpt-4o"
                       messages = [| {| role = "system"; content = "You are an expert Jetpack Compose frontend developer." |}
                                     {| role = "user"; content = prompt |} |] |}
                
                let json = System.Text.Json.JsonSerializer.Serialize(reqBody)
                let content = new System.Net.Http.StringContent(json, System.Text.Encoding.UTF8, "application/json")
                
                let! resp = client.PostAsync("https://api.openai.com/v1/chat/completions", content) |> Async.AwaitTask
                if resp.IsSuccessStatusCode then
                    let! respJson = resp.Content.ReadAsStringAsync() |> Async.AwaitTask
                    use doc = System.Text.Json.JsonDocument.Parse(respJson)
                    let text = doc.RootElement.GetProperty("choices").EnumerateArray() |> Seq.head |> fun c -> c.GetProperty("message").GetProperty("content").GetString()
                    // Clean markdown if AI included it
                    let clean = text.Replace("```kotlin", "").Replace("```", "").Trim()
                    return Some clean
                else
                    return None
            with _ ->
                return None
    }
