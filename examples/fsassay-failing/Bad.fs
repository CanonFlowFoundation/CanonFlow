module FsAssayFailingExample

let deliberatePartialAccess value =
    value |> Option.get
