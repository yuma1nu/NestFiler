using FilerApp.Core;
using Microsoft.UI.Xaml;
using Microsoft.UI.Xaml.Controls;
using Microsoft.UI.Xaml.Media;
using Microsoft.UI.Xaml.Input;
using Windows.ApplicationModel.DataTransfer;
using Windows.Storage;
namespace FilerApp;
public sealed partial class MainWindow
{
    private FavoriteNode? draggedNode;
    private FrameworkElement? favoriteDragSource;
    private Windows.Foundation.Point? favoritePointerStart;
    private bool favoriteDragStarting;
    private void InitializeFavoriteRootDrop()
    {
        Root.AddHandler(UIElement.PointerMovedEvent, new PointerEventHandler(async (_, args) =>
        {
            if (favoritePointerStart is not { } start || favoriteDragSource is not { } source || favoriteDragStarting) return;
            var point = args.GetCurrentPoint(Root);
            if (!point.Properties.IsLeftButtonPressed) { favoritePointerStart = null; return; }
            if (Math.Abs(point.Position.X - start.X) < 6 && Math.Abs(point.Position.Y - start.Y) < 6) return;
            favoritePointerStart = null; favoriteDragStarting = true; args.Handled = true;
            source.ReleasePointerCaptures();
            try { await source.StartDragAsync(args.GetCurrentPoint(source)); }
            catch (Exception ex) { ShowError($"ドラッグを開始できません: {ex.Message}"); }
            finally { favoriteDragStarting = false; favoriteDragSource = null; draggedNode = null; }
        }), true);
        Root.AddHandler(UIElement.PointerReleasedEvent, new PointerEventHandler((_, _) => favoritePointerStart = null), true);
        ToolTipService.SetToolTip(FavoriteRootTarget, "ここへドロップすると最上位へ移動／登録");
        AttachTreeDrop(FavoriteRootTarget, null, null, FavoriteRoots, null);
    }
    private void AttachTreeDrag(FrameworkElement row, FavoriteNode node)
    {
        row.CanDrag = true;
        row.AddHandler(UIElement.PointerPressedEvent, new PointerEventHandler((_, args) =>
        {
            var point = args.GetCurrentPoint(Root);
            if (!point.Properties.IsLeftButtonPressed) return;
            favoriteDragSource = row; favoritePointerStart = point.Position;
        }), true);        row.DragStarting += (_, args) => { favoritePointerStart = null; draggedNode = node; args.Data.SetText(node.Id); args.Data.RequestedOperation = DataPackageOperation.Move; };
        row.DropCompleted += (_, _) => draggedNode = null;
    }
    private void AttachTreeDrop(FrameworkElement row, FavoriteNode? target, FavoriteNode? parent, List<FavoriteNode> siblings, StackPanel? children)
    {
        row.AllowDrop = true;
        var expand = new DispatcherTimer { Interval = TimeSpan.FromMilliseconds(650) };
        expand.Tick += (_, _) => { expand.Stop(); if (target is { IsFolder: true } && children != null) { target.Expanded = true; children.Visibility = Visibility.Visible; ScheduleSave(); } };
        (FavoriteNode? Parent, string? Before, string Label, int Zone) Plan(DragEventArgs args)
        {
            if (target == null) return (null, null, "最上位へ移動／登録", 0);
            var fraction = args.GetPosition(row).Y / Math.Max(1, row.ActualHeight);
            if (target.IsFolder && fraction >= 0.25 && fraction <= 0.75) return (target, null, $"「{target.Name}」の中へ移動／登録", 0);
            bool before = fraction < 0.5;
            var index = siblings.IndexOf(target);
            var anchor = before ? target.Id : index + 1 < siblings.Count ? siblings[index + 1].Id : null;
            return (parent, anchor, $"「{target.Name}」の{(before ? "前" : "後")}へ移動／登録", before ? -1 : 1);
        }
        void Clear()
        {
            expand.Stop();
            if (row is Border border) { border.BorderThickness = new Thickness(0); border.Background = null; }
            if (row is Grid grid) grid.Background = null;
        }
        row.AddHandler(UIElement.DragOverEvent, new DragEventHandler((_, args) =>
        {
            args.Handled = true; var plan = Plan(args);
            bool accepted = draggedNode != null ? FavoriteTree.CanMove(FavoriteRoots, draggedNode.Id, plan.Parent?.Id) : args.DataView.Contains(StandardDataFormats.StorageItems);
            args.AcceptedOperation = !accepted ? DataPackageOperation.None : draggedNode != null ? DataPackageOperation.Move : DataPackageOperation.Copy;
            if (!accepted) { Clear(); args.DragUIOverride.Caption = "自分自身・子孫の中には移動できません"; return; }
            args.DragUIOverride.Caption = plan.Label;
            if (row is Border border)
            {
                border.BorderBrush = (Brush)Application.Current.Resources["AccentFillColorDefaultBrush"];
                border.BorderThickness = plan.Zone < 0 ? new Thickness(0, 2, 0, 0) : plan.Zone > 0 ? new Thickness(0, 0, 0, 2) : new Thickness(1);
                border.Background = plan.Zone == 0 ? (Brush)Application.Current.Resources["SubtleFillColorSecondaryBrush"] : null;
            }
            if (row is Grid grid) grid.Background = (Brush)Application.Current.Resources["SubtleFillColorSecondaryBrush"];
            if (plan.Parent == target && target is { Expanded: false } && !expand.IsEnabled) expand.Start();
            if (plan.Parent != target) expand.Stop();
        }), true);
        row.DragLeave += (_, _) => Clear();
        row.AddHandler(UIElement.DropEvent, new DragEventHandler(async (_, args) =>
        {
            args.Handled = true; var plan = Plan(args); var moving = draggedNode; var deferral = args.GetDeferral(); Clear();
            try
            {
                if (moving != null) FavoriteTree.Move(FavoriteRoots, moving.Id, plan.Parent?.Id, plan.Before);
                else if (args.DataView.Contains(StandardDataFormats.StorageItems))
                {
                    var folders = (await args.DataView.GetStorageItemsAsync()).OfType<StorageFolder>().ToArray();
                    var list = plan.Parent?.Children ?? FavoriteRoots;
                    var index = plan.Before == null ? list.Count : list.FindIndex(x => x.Id == plan.Before);
                    if (index < 0) throw new InvalidOperationException("移動先が変更されました。");
                    foreach (var folder in folders) list.Insert(index++, new() { Name = folder.Name, Path = folder.Path });
                }
                if (plan.Parent != null) plan.Parent.Expanded = true;
                args.AcceptedOperation = moving != null ? DataPackageOperation.Move : DataPackageOperation.Copy;
                RenderFavorites(); ScheduleSave();
            }
            catch (Exception ex) { args.AcceptedOperation = DataPackageOperation.None; ShowError(ex.Message); }
            finally { draggedNode = null; deferral.Complete(); }
        }), true);
    }
}



