using Microsoft.AspNetCore.Components.Forms;
using MudBlazor;
using Not.Notify;

namespace Not.Blazor.Components.Mud;

public partial class NImageBrowser
{
    List<string> _images = [];

    string ImagesFolder => Path.Combine(Environment.CurrentDirectory, Src);

    [Inject]
    IDialogService DialogService { get; set; } = default!;

    [Parameter]
    public EventCallback<string> OnImageSelected { get; set; }

    [Parameter]
    public string Src { get; set; } = default!;

    [Parameter]
    public string SelectedImage { get; set; } = default!;

    protected override void OnInitialized()
    {
        LoadImages();
    }

    void LoadImages()
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

    string GetBase64Image(string imagePath)
    {
        try
        {
            var imageBytes = File.ReadAllBytes(imagePath);
            var base64 = Convert.ToBase64String(imageBytes);
            var extension = Path.GetExtension(imagePath).ToLower().TrimStart('.');

            return $"data:image/{extension};base64,{base64}";
        }
        catch
        {
            return "";
        }
    }

    async Task SelectImage(string imagePath)
    {
        SelectedImage = imagePath;
        if (!string.IsNullOrEmpty(SelectedImage))
        {
            await OnImageSelected.InvokeAsync(imagePath);
        }
    }

    async Task OnFileSelected(IBrowserFile file)
    {
        if (file != null)
        {
            var fileName = file.Name + Path.GetExtension(file.Name);
            var destination = Path.Combine(ImagesFolder, fileName);

            await using var fs = File.Create(destination);
            await file.OpenReadStream(maxAllowedSize: 10_000_000).CopyToAsync(fs);

            LoadImages();
            StateHasChanged();
            NotifyHelper.Success("File uploaded successfully.");
        }
    }

    async Task PromptDeleteImage(string imagePath)
    {
        if (imagePath.EndsWith("blank.png", StringComparison.InvariantCulture))
        {
            NotifyHelper.Error("Cannot delete default image.");
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

    void DeleteImage(string imagePath)
    {
        try
        {
            if (File.Exists(imagePath))
            {
                File.Delete(imagePath);
                NotifyHelper.Warn("Image deleted.");
                LoadImages();
                StateHasChanged();
            }
            else
            {
                NotifyHelper.Error("File not found.");
            }
        }
        catch (Exception exception)
        {
            NotifyHelper.Error(exception);
        }
    }
}
