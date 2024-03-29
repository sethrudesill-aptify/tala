﻿namespace WcfStorm.Tala

open FsXaml
open Newtonsoft.Json
open RestSharp
open System
open System.Collections.ObjectModel
open System.Net
open System.Threading
open System.Windows.Input
open Xceed.Wpf.Toolkit

type SettingsWindow = XAML< "SettingsWindow.xaml" >

type MainWindowViewModel() =
    inherit NotifyBase()
    let requestPayload = HttpPayload()
    let responsePayload = HttpPayload()
    let mutable isCallInProgress = false
    let mutable httpCallFailed = false
    let mutable respTime = 0.0
    let mutable selectedTabIndex = 0
    let mutable isParametersChecked = true
    let mutable isRawBodyChecked = false
    let mutable selectedResponseCookie = RestResponseCookie()
    let verbs =
        let temp = ObservableCollection<Method>()
        temp.Add(Method.GET)
        temp.Add(Method.POST)
        temp.Add(Method.PUT)
        temp.Add(Method.DELETE)
        temp.Add(Method.HEAD)
        temp.Add(Method.OPTIONS)
        temp

    let mutable selectedVerb = Method.GET

    let urls =
        let temp = ObservableCollection<string>()
        temp.Add("http://www.reddit.com/r/programming.json")
        temp.Add("http://www.google.com")
        temp.Add("http://www.microsoft.com")
        temp

    let mutable targetUrl = urls.Item(0)
    let respHeaders = HttpHeaders()
    let respCookies = Cookies()

    let reqParams =
        let temp = HttpParams()
        temp.Add(HttpParam())
        temp

    let headers =
        let temp = HttpHeaders()
        temp.Add(WcfStorm.Tala.HttpHeader(Key = "User-Agent", Value = "WcfStorm.Rest/2.2.0 (.NET4.0);support@wcfstorm.com"))
        temp.Add(WcfStorm.Tala.HttpHeader(Key = "Content-Type", Value = "application/json"))
        temp.Add(WcfStorm.Tala.HttpHeader(Key = "", Value = ""))
        temp

    let parameterTypeSelection =
        let temp = new ObservableCollection<ParameterType>()
        temp.Add(ParameterType.UrlSegment)
        temp.Add(ParameterType.GetOrPost)
        temp

    let mutable statusCode = ""

    member this.TargetUrl
        with get () = targetUrl
        and set v = this.RaiseAndSetIfChanged(&targetUrl, v, "TargetUrl")

    member this.ResponseCookies = respCookies
    member this.RequestParameters = reqParams
    member this.RequestHeaders = headers
    member this.ResponseHeaders = respHeaders
    member this.Request = requestPayload
    member this.Response = responsePayload
    member this.Urls = urls
    member this.Verbs = verbs
    member this.ParameterTypeSelection = parameterTypeSelection

    member this.HttpCallFailed
        with get () = httpCallFailed
        and set v = this.RaiseAndSetIfChanged(&httpCallFailed, v, "HttpCallFailed")

    member this.SelectedVerb
        with get () = selectedVerb
        and set v = this.RaiseAndSetIfChanged(&selectedVerb, v, "SelectedVerb")

    member this.IsCallInProgress
        with get () = isCallInProgress
        and set v = this.RaiseAndSetIfChanged(&isCallInProgress, v, "IsCallInProgress")

    member this.ResponseStatusCode
        with get () = statusCode
        and set v = this.RaiseAndSetIfChanged(&statusCode, v, "ResponseStatusCode")

    member this.SelectedResponseCookie
        with get () = selectedResponseCookie
        and set v = this.RaiseAndSetIfChanged(&selectedResponseCookie, v, "SelectedResponseCookie")

    member this.AddRequestHeaderCommand = Command.create (fun arg -> not this.IsCallInProgress) (fun arg -> headers.Add(WcfStorm.Tala.HttpHeader(Key = "", Value = "")))
    member this.AddRequestParameterCommand = Command.create (fun arg -> not this.IsCallInProgress) (fun arg -> this.RequestParameters.Add(HttpParam(Name = "", Value = "")))

    member this.RemoveHeaderCommand =
        let onRun (arg : obj) =
            match Cast.convert<WcfStorm.Tala.HttpHeader> (arg) with
            | Some(reqHeader) ->
                this.RequestHeaders.Remove(reqHeader) |> ignore
                if (this.RequestHeaders.Count = 0) then (this.AddRequestHeaderCommand :> ICommand).Execute(null)
            | None -> ()
        Command.create (fun arg -> not this.IsCallInProgress) onRun

    member this.RemoveRequestParameterCommand =
        let onRun (arg : obj) =
            match Cast.convert<WcfStorm.Tala.HttpParam> (arg) with
            | Some(reqParam) ->
                this.RequestParameters.Remove(reqParam) |> ignore
                if (this.RequestParameters.Count = 0) then (this.AddRequestParameterCommand :> ICommand).Execute(null)
            | None -> ()
        Command.create (fun arg -> not this.IsCallInProgress) onRun

    member this.SaveCommand =
        let canRun arg = not this.IsCallInProgress
        Command.create canRun (fun arg ->
            let testReq = TestRequest(Url = this.TargetUrl, Verb = this.SelectedVerb, RequestText = "")
            testReq.RequestHeaders <- this.RequestHeaders
                                      |> Seq.filter (fun t -> not (String.IsNullOrWhiteSpace(t.Key)))
                                      |> Seq.toArray
            testReq.RequestParameters <- this.RequestParameters
                                         |> Seq.filter (fun t -> not (String.IsNullOrWhiteSpace(t.Name)))
                                         |> Seq.toArray
            testReq.RequestText <- this.Request.Doc.Text
            Core.saveTestData testReq)

    member this.OpenCommand =
        let canRun arg = not this.IsCallInProgress
        Command.create canRun (fun arg ->
            match Core.openTestData() with
            | Some(testReq) ->
                this.TargetUrl <- testReq.Url
                this.SelectedVerb <- testReq.Verb
                this.Request.Doc.Text <- testReq.RequestText
                this.RequestHeaders.Clear()
                testReq.RequestHeaders
                |> Seq.filter (fun t -> not (String.IsNullOrWhiteSpace(t.Key)))
                |> Seq.iter (fun h -> this.RequestHeaders.Add h)
                testReq.RequestParameters
                |> Seq.filter (fun t -> not (String.IsNullOrWhiteSpace(t.Name)))
                |> Seq.iter (fun h -> this.RequestParameters.Add h)
            | None -> ())

    member this.SettingsCommand =
        let canRun arg = not this.IsCallInProgress
        Command.create canRun (fun arg ->
            let win = SettingsWindow()
            let vm = win.Root.DataContext :?> SettingsWindowViewModel
            vm.Close <- win.Root.Close
            win.Root.Owner <- System.Windows.Application.Current.MainWindow
            win.Root.ShowDialog() |> ignore)

    member this.ResponseTimeInSec
        with get () = respTime
        and set v = this.RaiseAndSetIfChanged(&respTime, v, "ResponseTimeInSec")

    member this.SendCommand =
        let processResp (rawResponse : IRestResponse) elapsed =
            let processed = Core.processRestResp rawResponse elapsed
            this.ResponseStatusCode <- processed.ResponseCode
            this.HttpCallFailed <- processed.HttpCallFailed
            this.Response.SetText(processed.RawResponseText, processed.HttpContentType)
            this.Response.Mode <- processed.HttpContentType
            this.IsCallInProgress <- false
            this.ResponseTimeInSec <- processed.Elapsed.TotalSeconds

            respCookies.Clear()
            for c in processed.Cookies do
                respCookies.Add c
            
            if respCookies.Count > 0 then
                this.SelectedResponseCookie <- respCookies.Item(0);  

            respHeaders.Clear()
            for h in processed.Headers do
                respHeaders.Add h

        let canRun arg = not this.IsCallInProgress
        let onOk (resp, elapsed) = processResp resp elapsed

        let onError exc =
            this.IsCallInProgress <- false
            this.Response.Doc.Text <- exc.ToString()

        let onCancel arg = this.Response.Doc.Text <- "Operation canceled."

        let getTargetUrl() =
            match Core.getUri this.TargetUrl with
            | Some(uri) ->
                let trimmed = uri.AbsoluteUri.TrimEnd('/')
                if not (urls.Contains(trimmed)) then urls.Add(trimmed)
                trimmed
            | None -> ""
        Command.create canRun (fun arg ->
            this.IsCallInProgress <- true
            this.HttpCallFailed <- false
            Async.StartWithContinuations(Core.runAsync (getTargetUrl()) "/" this.RequestParameters this.RequestHeaders this.SelectedVerb this.Request.Doc.Text, onOk, onError, onCancel))