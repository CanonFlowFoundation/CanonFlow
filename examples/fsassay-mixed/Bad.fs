module MixedBad

let deliberatePartialAccess value =
    value |> Option.get
