using System.ComponentModel.DataAnnotations;
using Microsoft.AspNetCore.Mvc.ModelBinding;
using Microsoft.AspNetCore.Mvc.ModelBinding.Metadata;

namespace BookStore.Web.Infrastructure;

// MVC luôn tự thêm [Required] cho kiểu giá trị không nullable (int, decimal...). Gán thông báo
// song ngữ của dự án để không lộ câu mặc định tiếng Anh "The X field is required" trong HTML/API.
public sealed class LocalizedRequiredMetadata : IValidationMetadataProvider
{
    public void CreateValidationMetadata(ValidationMetadataProviderContext context)
    {
        // Kiểu giá trị không nullable: MVC tự sinh [Required] ở bước client-validator với câu tiếng Anh;
        // khai báo sẵn một [Required] song ngữ để nó được dùng thay thế (data-val-required).
        var type = context.Key.ModelType;
        // Áp dụng cả bool/enum: MVC vốn đã gắn [Required] ngầm cho chúng, ở đây chỉ thay câu thông báo
        // sang song ngữ. Required không làm checkbox chưa tick bị từ chối (false vẫn là giá trị hợp lệ).
        if (context.Key.MetadataKind == ModelMetadataKind.Property && type.IsValueType && Nullable.GetUnderlyingType(type) is null
            && !context.ValidationMetadata.ValidatorMetadata.OfType<RequiredAttribute>().Any())
            context.ValidationMetadata.ValidatorMetadata.Add(new RequiredAttribute());
        foreach (var required in context.ValidationMetadata.ValidatorMetadata.OfType<RequiredAttribute>())
            if (required.ErrorMessage is null && required.ErrorMessageResourceType is null)
            {
                required.ErrorMessageResourceType = typeof(ValidationText);
                required.ErrorMessageResourceName = nameof(ValidationText.Required);
            }
    }
}
