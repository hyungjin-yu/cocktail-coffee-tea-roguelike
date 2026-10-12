using System;
using NUnit.Framework;

namespace CCT.Tests
{
    /// <summary>검증 전용 도구(부하 시나리오·입력 대행·자동 투어/오디오 탭)는 검증 빌드 심볼에서만 컴파일된다 — 일반 프리뷰/출시 런타임
    /// 어셈블리에는 타입이 없어야 한다(26 단계 5 "검증 전용 도구가 일반 배포에 포함되지 않음").</summary>
    public class QaToolIsolationTests
    {
        [TestCase("CCT.LoadScenario")]
        [TestCase("CCT.PlayVerifier")]
        [TestCase("CCT.AutoTour")]
        [TestCase("CCT.QaAudioTap")]
        public void QaOnlyType_IsNotCompiledIntoRuntimeAssembly(string typeName)
        {
            var runtime = typeof(GameManager).Assembly;
            Assert.IsNull(runtime.GetType(typeName, false), $"{typeName}가 일반 CCT.Runtime에 포함됨");
            Assert.IsNotNull(runtime.GetType("CCT.LaunchOptions", false), "기준 타입 확인");
        }
    }
}
