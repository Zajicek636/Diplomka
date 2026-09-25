using Volo.Abp.Domain;
using Volo.Abp.Modularity;

namespace TestGenerator.Sample;

[DependsOn(typeof(AbpDddDomainModule))]
public class EshopDomainModule : AbpModule
{
}
