using AutoMapper;
using IsoDoc.Application.Common;
using IsoDoc.Application.Common.Models;
using IsoDoc.Domain.Entities;

namespace IsoDoc.Application.Mappings;

public sealed class DocumentMappingProfile : Profile
{
    public DocumentMappingProfile()
    {
        CreateMap<Document, DocumentDto>()
            .ForMember(dst => dst.DocumentCode, opt => opt.MapFrom(src => src.Code.Value))
            .ForMember(dst => dst.IsoStandard, opt => opt.MapFrom(src => src.Standard.ToString()))
            .ForMember(dst => dst.Category, opt => opt.MapFrom(src => src.Category.ToString()))
            .ForMember(dst => dst.Status, opt => opt.MapFrom(src => src.Status.ToString()))
            .ForMember(dst => dst.CurrentVersion, opt => opt.MapFrom(src => src.CurrentVersion.ToString()))
            .ForMember(dst => dst.OwnerName, opt => opt.Ignore())
            .ForMember(dst => dst.DepartmentName, opt => opt.Ignore())
            .ForMember(dst => dst.ActiveWorkflow, opt => opt.Ignore());

        CreateMap<Document, DocumentSummaryDto>()
            .ForMember(dst => dst.DocumentCode, opt => opt.MapFrom(src => src.Code.Value))
            .ForMember(dst => dst.IsoStandard, opt => opt.MapFrom(src => src.Standard.ToString()))
            .ForMember(dst => dst.Category, opt => opt.MapFrom(src => src.Category.ToString()))
            .ForMember(dst => dst.Status, opt => opt.MapFrom(src => src.Status.ToString()))
            .ForMember(dst => dst.CurrentVersion, opt => opt.MapFrom(src => src.CurrentVersion.ToString()))
            .ForMember(dst => dst.OwnerName, opt => opt.Ignore())
            .ForMember(dst => dst.HighlightedFragments, opt => opt.Ignore());

        CreateMap<DocumentVersion, DocumentVersionDto>()
            .ForMember(dst => dst.FileType, opt => opt.MapFrom(src => src.FileType.ToString()))
            .ForMember(dst => dst.OriginalFileName, opt => opt.MapFrom(src => DocumentBlobPathParser.OriginalFileName(src.BlobPath)))
            .ForMember(dst => dst.ContentType, opt => opt.MapFrom(src => DocumentFileMime.ForFileType(src.FileType)))
            .ForMember(dst => dst.FileSize, opt => opt.MapFrom(src => src.FileSize))
            .ForMember(dst => dst.FileSizeFormatted, opt => opt.MapFrom(src => src.FileSizeFormatted))
            .ForMember(dst => dst.ChecksumHex, opt => opt.MapFrom(src => src.Checksum.Value))
            .ForMember(dst => dst.UploadedByName, opt => opt.Ignore());

        CreateMap<ApprovalWorkflow, WorkflowStatusDto>()
            .ForMember(dst => dst.WorkflowId, opt => opt.MapFrom(src => src.Id))
            .ForMember(dst => dst.Status, opt => opt.MapFrom(src => src.Status.ToString()))
            .ForMember(dst => dst.TotalSteps, opt => opt.MapFrom(src => src.Steps.Count))
            .ForMember(dst => dst.Steps, opt => opt.MapFrom(src => src.Steps.OrderBy(s => s.StepOrder)))
            .ForMember(dst => dst.CurrentApproverName, opt => opt.Ignore());

        CreateMap<ApprovalWorkflow.ApprovalStep, ApprovalStepDto>()
            .ForMember(dst => dst.Decision, opt => opt.MapFrom(src => src.Decision.ToString()))
            .ForMember(dst => dst.ApproverName, opt => opt.Ignore());
    }

}

