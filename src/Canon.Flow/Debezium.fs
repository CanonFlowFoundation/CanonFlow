namespace Canon.Flow

// post-v1 sequencing — do not extend until Phase 5 gate

open System
open System.Text.Json
open FsCodec.SystemTextJson
open Canon.Introspect

module Debezium =

    // Standard Debezium Envelope
    type Payload<'T> = {
        before: 'T option
        after: 'T option
        op: string // 'c' (create), 'u' (update), 'd' (delete), 'r' (read)
        ts_ms: int64
    }

    type Envelope<'T> = {
        payload: Payload<'T>
    }

    /// Converts a raw JSON Debezium message into a strongly typed Envelope
    let parse<'T> (json: string) : Envelope<'T> =
        let options = Options.Create()
        JsonSerializer.Deserialize<Envelope<'T>>(json, options)
        
module Events =

    /// The core events representing our system's reaction to CDC
    type FlowEvent =
        | SchemaDiscovered of TableDef
        | SchemaDropped of string
        
    /// FsCodec allows us to easily serialize/deserialize our domain events
    /// so they can be natively stored in EventStore or Kafka.
    let codec = Codec.Create<FlowEvent>()
