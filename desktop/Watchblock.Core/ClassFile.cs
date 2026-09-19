using System.Buffers.Binary;using System.Text;
namespace Watchblock.Core;
public static class ClassFile {
 public static string Constants(byte[] bytes,bool collectStrings=true){int at=0;void Need(int count){if(count<0||at>bytes.Length-count)throw new InvalidDataException("클래스 구조 오류");}ushort U2(){Need(2);var n=BinaryPrimitives.ReadUInt16BigEndian(bytes.AsSpan(at,2));at+=2;return n;}void Skip(int n){Need(n);at+=n;}
  Need(10);if(BinaryPrimitives.ReadUInt32BigEndian(bytes)!=0xcafebabe)throw new InvalidDataException("클래스 서명 오류");at=8;int count=U2();if(count==0)throw new InvalidDataException();var values=new StringBuilder();for(int i=1;i<count;i++){Need(1);byte tag=bytes[at++];switch(tag){case 1:int len=U2();Need(len);if(collectStrings)values.Append(Encoding.UTF8.GetString(bytes,at,len)).Append('\n');at+=len;break;case 3:case 4:Skip(4);break;case 5:case 6:Skip(8);if(++i>=count)throw new InvalidDataException();break;case 7:case 8:case 16:case 19:case 20:Skip(2);break;case 9:case 10:case 11:case 12:case 17:case 18:Skip(4);break;case 15:Skip(3);break;default:throw new InvalidDataException("지원하지 않는 클래스 상수");}}Need(6);return values.ToString();
 }
}
