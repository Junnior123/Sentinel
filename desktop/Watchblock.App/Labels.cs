using System.Globalization;using System.Windows.Data;
namespace Watchblock.App;
public sealed class Labels:IValueConverter {
 public object Convert(object value,Type targetType,object parameter,CultureInfo culture)=>value?.ToString() switch{"complete"=>"완료","partial"=>"부분 완료","skipped"=>"제외","error"=>"오류","changed"=>"변경됨","known"=>"치트 탐지","review"=>"검토 필요","policy"=>"서버 규정","denied"=>"권한 부족","unsupported"=>"미지원","cancelled"=>"취소","failed"=>"실패","recycle-bin"=>"휴지통","usn"=>"삭제 저널","process"=>"프로세스","path-only"=>"경로 일치","basename-only"=>"이름 일치","none"=>"연결 없음","files"=>"파일","hardware"=>"하드웨어 변경","boot"=>"부팅 정책","boot-integrity"=>"코드 무결성","all-files"=>"전체 파일",_=>value??""};
 public object ConvertBack(object value,Type targetType,object parameter,CultureInfo culture)=>throw new NotSupportedException();
}
