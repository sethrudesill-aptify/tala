﻿namespace WcfStorm.Tala

open ICSharpCode.AvalonEdit.Highlighting
open ICSharpCode.AvalonEdit
open System.Text
open System.IO
open System.Resources
open System.Xml
open ICSharpCode.AvalonEdit.Document
open ICSharpCode.AvalonEdit.Folding
open Newtonsoft.Json
open System.Xml.Linq

type HttpPayload() =
    inherit NotifyBase()
    let mutable highlighting = Resource.jsonHighlightingMode
    let mutable mode = HttpContentType.Json("text/json")
    let mutable doc = TextDocument(Text="")
    let mutable foldFunction = 
        let folding, _ = EditorOptions.get mode
        folding
  
    let prettyPrintJson(json) =
        try
            use stringReader = new StringReader(json)
            use stringWriter = new StringWriter()
            use jsonReader = new JsonTextReader(stringReader)
            use jsonWriter = new JsonTextWriter(stringWriter, Formatting = Newtonsoft.Json.Formatting.Indented)
            jsonWriter.WriteToken(jsonReader)
            stringWriter.ToString()
        with
        | _ -> json
        
    let prettyPrintXml(xml) =
        try
            XDocument.Parse(xml).ToString()
        with
        | _ -> xml

    member this.Mode 
        with get() = mode
        and set v =
            if not(mode = v) then
                let folding, highlighting2 =  EditorOptions.get v
                this.Highlighting <- highlighting2
                foldFunction <- folding 
    member this.Highlighting 

        with get () = highlighting
        and set v = this.RaiseAndSetIfChanged(&highlighting, v, "Highlighting")
    
    member this.Doc 
        with get () = doc
        and set v = this.RaiseAndSetIfChanged(&doc, v, "Doc")

    member x.FoldFunction = foldFunction

    member this.SetText(text, mode:HttpContentType) =
        match mode with
        | Json(_) -> this.Doc.Text <- prettyPrintJson text
        | Xml(_) -> this.Doc.Text <- prettyPrintXml text
        | _ -> this.Doc.Text <- text
            
