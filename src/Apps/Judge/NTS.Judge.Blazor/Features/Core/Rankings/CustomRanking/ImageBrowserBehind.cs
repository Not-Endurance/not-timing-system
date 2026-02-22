using Microsoft.AspNetCore.Components.Forms;
using MudBlazor;
using Not.Blazor.Components.Abstractions;
using Not.Notify;

namespace NTS.Judge.Blazor.Features.Core.Rankings.CustomRanking;

public class ImageBrowserBehind : NComponent
{
    List<string> _images = [];

    [Inject]
    IDialogService DialogService { get; set; } = default!;

    [Inject]
    INotifier Notifier { get; set; } = default!;

    string ImagesFolder => Path.Combine(Environment.CurrentDirectory, Src);

    protected IReadOnlyList<string> Images => _images;

    [Parameter]
    public EventCallback<string> OnImageSelected { get; set; }

    [Parameter]
    public string Src { get; set; } = default!;

    [Parameter]
    public string SelectedImage { get; set; } = default!;

    protected override void OnInitialized()
    {
        try
        {
            LoadImages();
        }
        catch (Exception ex)
        {
            Handle(ex);
        }
    }

    protected void LoadImages()
    {
        if (!Directory.Exists(ImagesFolder))
        {
            Directory.CreateDirectory(ImagesFolder);
        }

        _images = Directory
            .GetFiles(ImagesFolder)
            .Where(f => new[] { ".png", ".jpg", ".jpeg", ".gif" }.Contains(Path.GetExtension(f).ToLower()))
            .ToList();
    }

    protected string GetBase64Image(string imagePath)
    {
        try
        {
            var imageBytes = File.ReadAllBytes(imagePath);
            var base64 = Convert.ToBase64String(imageBytes);
            var extension = Path.GetExtension(imagePath).ToLower().TrimStart('.');

            return $"data:image/{extension};base64,{base64}";
        }
        catch (Exception ex)
        {
            Handle(ex);
            return string.Empty;
        }
    }

    protected async Task SelectImage(string imagePath)
    {
        try
        {
            SelectedImage = imagePath;
            if (!string.IsNullOrEmpty(SelectedImage))
            {
                await OnImageSelected.InvokeAsync(imagePath);
            }
        }
        catch (Exception ex)
        {
            Handle(ex);
        }
    }

    protected async Task OnFileSelected(IBrowserFile? file)
    {
        try
        {
            if (file == null)
            {
                return;
            }

            var fileName = Path.GetFileName(file.Name);
            var destination = Path.Combine(ImagesFolder, fileName);

            await using var fs = File.Create(destination);
            await file.OpenReadStream(maxAllowedSize: 10_000_000).CopyToAsync(fs);

            LoadImages();
            await InvokeRender();
            Notifier.Success("File uploaded successfully.");
        }
        catch (Exception ex)
        {
            Handle(ex);
        }
    }

    protected async Task PromptDeleteImage(string imagePath)
    {
        try
        {
            if (imagePath.EndsWith("blank.png", StringComparison.InvariantCulture))
            {
                Notifier.Error("Cannot delete default image.");
                return;
            }

            var confirmed = await DialogService.ShowMessageBox(
                "Confirm Delete",
                $"Are you sure you want to delete '{Path.GetFileName(imagePath)}'?",
                yesText: "Delete",
                cancelText: "Cancel"
            );

            if (confirmed == true)
            {
                DeleteImage(imagePath);
            }
        }
        catch (Exception ex)
        {
            Handle(ex);
        }
    }

    protected void DeleteImage(string imagePath)
    {
        try
        {
            if (File.Exists(imagePath))
            {
                File.Delete(imagePath);
                Notifier.Warn("Image deleted.");
                LoadImages();
                _ = InvokeRender();
            }
            else
            {
                Notifier.Error("File not found.");
            }
        }
        catch (Exception ex)
        {
            Handle(ex);
        }
    }
}
