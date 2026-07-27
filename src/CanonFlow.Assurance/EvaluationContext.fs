namespace CanonFlow.Assurance

open System

type TimeProvenance =
    | Declared
    | SignedCapture of Digest
    | TrustedTimestamp of string

type EvaluationInstant = private EvaluationInstant of DateTimeOffset

module EvaluationInstant =
    let ofDateTimeOffset (dto: DateTimeOffset) = EvaluationInstant dto
    let toDateTimeOffset (EvaluationInstant dto) = dto

type EvaluationBudget =
    { MaxFiles: int
      MaxInputBytes: int64
      MaxJsonDepth: int
      ComponentTimeoutSeconds: int
      TotalTimeoutSeconds: int }

type NetworkPolicy =
    | Forbidden
    | Allowed

type EvaluationContext =
    { Instant: EvaluationInstant
      TimeProvenance: TimeProvenance
      Locale: string
      NetworkPolicy: NetworkPolicy
      Budget: EvaluationBudget }

