namespace Fabulous.XamarinForms

open System
open System.Collections
open System.Collections.Generic
open Fabulous
open Fabulous.XamarinForms
open Xamarin.Forms

type GroupItem(header: Widget, footer: Widget, source: IEnumerable<Widget>) =
    member _.Header = header
    member _.Footer = footer
    interface IEnumerable<Widget> with
        member this.GetEnumerator(): IEnumerator<Widget> = source.GetEnumerator()
        member this.GetEnumerator(): IEnumerator = source.GetEnumerator()

type VirtualizedItemType =
    | Header
    | Footer
    | Item
    
module BindableHelpers =
    /// On BindableContextChanged triggered, call the Reconciler to update the cell
    let createOnBindingContextChanged canReuseView (itemType: VirtualizedItemType) (target: BindableObject) =
        let mutable prevWidgetOpt: Widget voption = ValueNone
        
        let onBindingContextChanged () =
            match target.BindingContext with
            | null -> ()
            | value ->
                let currWidget =
                    match itemType with
                    | Item -> value :?> Widget
                    | Header -> (value :?> GroupItem).Header
                    | Footer -> (value :?> GroupItem).Footer
                    
                let node = ViewNode.get target
                Reconciler.update canReuseView prevWidgetOpt currWidget node
                prevWidgetOpt <- ValueSome currWidget

        onBindingContextChanged

/// Create a DataTemplate for a specific root type (TextCell, ViewCell, etc.)
/// that listen for BindingContext change to apply the Widget content to the cell
type WidgetDataTemplate(``type``, itemType, parent: IViewNode) =
    inherit DataTemplate(fun () ->
        let bindableObject = Activator.CreateInstance ``type`` :?> BindableObject
        
        let viewNode = ViewNode(ValueSome parent, parent.TreeContext, WeakReference(bindableObject))
        bindableObject.SetValue(ViewNode.ViewNodeProperty, viewNode)
        
        let onBindingContextChanged = BindableHelpers.createOnBindingContextChanged parent.TreeContext.CanReuseView itemType bindableObject
        bindableObject.BindingContextChanged.Add (fun _ -> onBindingContextChanged ())
        
        bindableObject :> obj
    )
        

/// Redirect to the right type of DataTemplate based on the target type of the current widget cell
type WidgetDataTemplateSelector internal (node: IViewNode, itemType: VirtualizedItemType, getWidget: obj -> Widget) =
    inherit DataTemplateSelector()
    
    /// Reuse data template for already known widget target type
    let cache = Dictionary<Type, WidgetDataTemplate>()
    
    override _.OnSelectTemplate(item, _) =
        let widget = getWidget item
        let widgetDefinition = WidgetDefinitionStore.get widget.Key
        let targetType = widgetDefinition.TargetType
        match cache.TryGetValue(targetType) with
        | true, dataTemplate -> dataTemplate
        | false, _ ->
            let dataTemplate = WidgetDataTemplate(targetType, itemType, node)
            cache.Add(targetType, dataTemplate)
            dataTemplate

type SimpleWidgetDataTemplateSelector(node: IViewNode) =
    inherit WidgetDataTemplateSelector(node, Item, fun item -> item :?> Widget)
        
type GroupedWidgetDataTemplateSelector(node: IViewNode, itemType: VirtualizedItemType) =
    inherit WidgetDataTemplateSelector(node, itemType, fun item ->
        let groupItem = item :?> GroupItem
        if itemType = Header then groupItem.Header else groupItem.Footer
    )
        
type WidgetItems<'T> =
    { OriginalItems: IEnumerable<'T>
      Template: 'T -> Widget }
    
type GroupedWidgetItems<'T> =
    { OriginalItems: IEnumerable<'T>
      Template: 'T -> GroupItem }