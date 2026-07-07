namespace Canon.Cli

open Canon.Introspect

module ReactScaffold =
    
    let generateForm (table: TableDef) : string =
        let tableName = table.Name
        let componentName = $"{tableName.Substring(0, 1).ToUpper()}{tableName.Substring(1)}Form"
        
        let imports = 
            [ "import React from 'react';"
              "import { useForm } from 'react-hook-form';"
              "import * as Validators from '../validators';" ]
            |> String.concat "\n"

        let inputs = 
            table.Columns
            |> List.map (fun c -> 
                let inputType = 
                    match c.DataType.ToLower() with
                    | "integer" | "numeric" | "decimal" -> "number"
                    | _ -> "text"
                
                let registerCall =
                    if c.CheckConstraints.IsEmpty then
                        $"{{...register('{c.Name}')}}"
                    else
                        $"{{...register('{c.Name}', {{ validate: (v, formValues) => Validators.validate_{tableName}_{c.Name}(formValues) || 'Invalid value' }})}}"

                $"""      <div className="mb-4">
        <label className="block text-sm font-medium text-gray-700">{c.Name}</label>
        <input 
          type="{inputType}" 
          {registerCall} 
          className="mt-1 block w-full rounded-md border-gray-300 shadow-sm focus:border-indigo-500 focus:ring-indigo-500 sm:text-sm transition duration-150 ease-in-out hover:border-blue-400"
        />
        {{errors.{c.Name} && <p className="text-red-500 text-xs mt-1">{{(errors.{c.Name} as any).message}}</p>}}
      </div>"""
            )
            |> String.concat "\n"

        $"""{imports}

export default function {componentName}() {{
  const {{ register, handleSubmit, formState: {{ errors }} }} = useForm();

  const onSubmit = (data: any) => {{
    console.log("Validated Data:", data);
  }};

  return (
    <div className="max-w-xl mx-auto mt-10">
      <form onSubmit={{handleSubmit(onSubmit)}} className="p-8 bg-white/80 backdrop-blur-md rounded-2xl shadow-xl border border-gray-100">
        <div className="mb-8">
          <h2 className="text-3xl font-extrabold text-transparent bg-clip-text bg-gradient-to-r from-blue-600 to-indigo-600 tracking-tight">{componentName}</h2>
          <p className="text-gray-500 mt-2 text-sm">Mathematically sound data entry.</p>
        </div>
        
{inputs}

        <button type="submit" className="w-full mt-6 px-4 py-3 font-semibold bg-gradient-to-r from-blue-600 to-indigo-600 text-white rounded-xl shadow-md hover:from-blue-700 hover:to-indigo-700 focus:outline-none focus:ring-2 focus:ring-indigo-500 focus:ring-offset-2 transition-all transform hover:scale-[1.02]">
          Submit Securely
        </button>
      </form>
    </div>
  );
}}
"""

    let tryGenerateSmartFormAsync (table: TableDef) : Async<string option> = async {
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
You are an expert React and Tailwind developer. 
I need a beautiful, premium, highly styled React Hook Form component for a PostgreSQL table named "{tableName}".
Here is the schema and constraints for the table:
{schemaJson}

Requirements:
1. Output ONLY valid TSX code, no markdown wrappers, no explanations.
2. Use modern Tailwind UI (vibrant gradients, glassmorphism, hover effects, floating labels).
3. The component MUST import its mathematical validators via `import * as Validators from '../validators';`
4. The form must register fields like `{{...register('column_name', {{ validate: (v, formValues) => Validators.validate_tableName_columnName(formValues) || 'Invalid value' }})}}` if constraints exist.
5. Create a stunning user experience that feels state-of-the-art.
"""
            try
                use client = new System.Net.Http.HttpClient()
                client.DefaultRequestHeaders.Add("Authorization", $"Bearer {apiKey}")
                
                let reqBody = 
                    {| model = "gpt-4o"
                       messages = [| {| role = "system"; content = "You are an expert React/Tailwind frontend developer." |}
                                     {| role = "user"; content = prompt |} |] |}
                
                let json = System.Text.Json.JsonSerializer.Serialize(reqBody)
                let content = new System.Net.Http.StringContent(json, System.Text.Encoding.UTF8, "application/json")
                
                let! resp = client.PostAsync("https://api.openai.com/v1/chat/completions", content) |> Async.AwaitTask
                if resp.IsSuccessStatusCode then
                    let! respJson = resp.Content.ReadAsStringAsync() |> Async.AwaitTask
                    use doc = System.Text.Json.JsonDocument.Parse(respJson)
                    let text = doc.RootElement.GetProperty("choices").EnumerateArray() |> Seq.head |> fun c -> c.GetProperty("message").GetProperty("content").GetString()
                    // Clean markdown if AI included it
                    let clean = text.Replace("```tsx", "").Replace("```typescript", "").Replace("```", "").Trim()
                    return Some clean
                else
                    return None
            with _ ->
                return None
    }

