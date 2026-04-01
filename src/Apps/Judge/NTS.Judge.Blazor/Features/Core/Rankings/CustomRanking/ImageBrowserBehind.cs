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
            Notifier.Success(File_uploaded_successfully_string);
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
                Notifier.Error(Cannot_delete_default_image_string);
                return;
            }

            var confirmed = await DialogService.ShowMessageBox(
                Confirm_action_string,
                string.Format(Are_you_sure_you_want_to_remove__the_snapshot_will_be_lost_string, Path.GetFileName(imagePath)),
                yesText: Delete_string,
                cancelText: Cancel_string
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
                Notifier.Warn(Image_deleted_string);
                LoadImages();
                _ = InvokeRender();
            }
            else
            {
                Notifier.Error(File_not_found_string);
            }
        }
        catch (Exception ex)
        {
            Handle(ex);
        }
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
}
