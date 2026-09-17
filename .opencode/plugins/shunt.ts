import { type Plugin, tool } from "@opencode-ai/plugin"
import * as fs from "fs"
import * as path from "path"

// ---------------------------------------------------------------------------
// Config (env — repo: opencode.json / usuario: ~/.config/opencode/opencode.jsonc)
// ---------------------------------------------------------------------------
const HF_API_KEY = process.env.HF_API_KEY
const HF_MODEL = process.env.HF_MODEL || "Qwen/Qwen3.8-27B:ovhcloud"
const MIN_LINES = parseInt(process.env.SHUNT_MIN_LINES || "350", 10)

// ---------------------------------------------------------------------------
// Llamada a Hugging Face Router (OpenAI-compatible)
// ---------------------------------------------------------------------------
async function callHF(systemInstruction: string, userText: string): Promise<string> {
  if (!HF_API_KEY) {
    throw new Error(
      "HF_API_KEY no está definida. Expórtala en ~/.config/opencode/opencode.jsonc -> env.HF_API_KEY (hf_...). Ver https://huggingface.co/settings/tokens"
    )
  }

  const url = "https://router.huggingface.co/v1/chat/completions"

  const body = {
    model: HF_MODEL,
    messages: [
      { role: "system", content: systemInstruction },
      { role: "user", content: userText },
    ],
    temperature: 0.2,
    max_tokens: 2000,
  }

  const res = await fetch(url, {
    method: "POST",
    headers: {
      "Content-Type": "application/json",
      Authorization: `Bearer ${HF_API_KEY}`,
    },
    body: JSON.stringify(body),
  })

  if (!res.ok) {
    const errText = await res.text()
    throw new Error(`HF Router error (${res.status}): ${errText}`)
  }

  // eslint-disable-next-line @typescript-eslint/no-explicit-any
  const data: any = await res.json()

  // log tokens a stderr (no consume contexto del modelo principal)
  if (data.usage) {
    console.error(
      `[hf_shunt] ${HF_MODEL} tokens -> prompt: ${data.usage.prompt_tokens}, completion: ${data.usage.completion_tokens}, total: ${data.usage.total_tokens}`
    )
  }

  // Soporta tanto `choices[0].message.content` como `reasoning` separado (Qwen)
  const text: string = data.choices?.[0]?.message?.content ?? ""
  return text.trim()
}

function stripFences(text: string): string {
  return text
    .replace(/^```[a-zA-Z0-9]*\n/, "")
    .replace(/```\s*$/, "")
    .trim()
}

function countLines(absPath: string): number {
  try {
    return fs.readFileSync(absPath, "utf-8").split("\n").length
  } catch {
    return 0
  }
}

function readFileSafe(absPath: string): string {
  return fs.readFileSync(absPath, "utf-8")
}

// ---------------------------------------------------------------------------
// Instrucción — solo lectura/resumen (ayudante que lee por ti para bajar tokens)
// ---------------------------------------------------------------------------
const BULK_READER_INSTRUCTIONS = `You are a precise code analyst. Read the provided files and answer the question concisely. Output structured bullets only. No greetings, no prose, no preambles. Lead every bullet with the exact name, type, or line number. Use nested bullets for details. Skip anything the caller did not ask for. Keep it short to save tokens.`

// ---------------------------------------------------------------------------
// Plugin — solo bulk_read (+ hook shunt)
// ---------------------------------------------------------------------------
export const ShuntPlugin: Plugin = async ({ directory }) => {
  return {
    tool: {
      bulk_read: tool({
        description:
          "Delega la lectura de uno o más archivos (cualquier tamaño) a un modelo pequeño (Qwen 27B vía Hugging Face Router) y devuelve un resumen conciso en bullets. Úsalo cuando solo necesites entender/resumir sin traer el archivo completo al contexto — ahorra 80-90% tokens. Para >350 líneas es obligatorio (el hook te bloquea el read); para archivos pequeños (<350) es opcional pero recomendado si tu pregunta cabe en 5-10 bullets.",
        args: {
          question: tool.schema.string().describe("La pregunta específica a responder sobre los archivos"),
          paths: tool.schema
            .string()
            .describe("Lista de rutas de archivo separadas por coma o salto de línea"),
        },
        async execute(args) {
          const paths = args.paths
            .split(/[\n,]/)
            .map((p) => p.trim())
            .filter(Boolean)

          const blocks = paths.map((p) => {
            const abs = path.resolve(directory, p)
            const content = readFileSafe(abs)
            return `<file path="${p}">\n${content}\n</file>`
          })

          const userText = `${blocks.join("\n\n")}\n\nQuestion: ${args.question}`
          const raw = await callHF(BULK_READER_INSTRUCTIONS, userText)
          return stripFences(raw)
        },
      }),
    },

    // Hook: bloquea lecturas grandes y redirige a bulk_read (ahorro de tokens)
    "tool.execute.before": async (input, output) => {
      if (input.tool === "read") {
        const filePath = output.args.filePath as string | undefined
        if (!filePath) return
        if (output.args.offset || output.args.limit) return
        const abs = path.resolve(directory, filePath)
        const lines = countLines(abs)
        if (lines > MIN_LINES) {
          throw new Error(
            `El archivo tiene ${lines} líneas (> ${MIN_LINES}). No lo leas directo — usa la tool bulk_read con esta ruta y tu pregunta específica. Es mucho más barato (HF Qwen).`
          )
        }
      }

      if (input.tool === "bash") {
        const cmd = (output.args.command as string) || ""
        if (/\|/.test(cmd)) return
        const match = cmd.match(/\b(cat|head|tail|less|more)\s+([^\s;&]+)/)
        if (match) {
          const target = match[2]
          const abs = path.resolve(directory, target)
          const lines = countLines(abs)
          if (lines > MIN_LINES) {
            throw new Error(
              `${target} tiene ${lines} líneas (> ${MIN_LINES}). Usa la tool bulk_read en vez de '${match[1]}' (HF Qwen).`
            )
          }
        }
      }
    },
  }
}
