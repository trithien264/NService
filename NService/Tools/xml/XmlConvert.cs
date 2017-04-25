/*將xml字符串轉成通用對象,ArrayList,Dictionary<string,object>*/
using System;
using System.Collections.Generic;
using System.Text;
using System.Collections;
using System.Xml;

namespace NService.Tools
{
    class XmlConvert
    {
        //某個結點的PCC_TYPE attribute為LIST，表示是一個ArrayList
        const string TAG_TYPE_NAME = "PCCTYPE";
        const string TYPE_LIST = "PCCLIST";

        /// <summary>
        /// 將一個xml文檔轉成Dictionary<string,object>對象，根目錄下即是欄位鍵值對
        /// </summary>
        /// <param name="xmlStr"></param>
        /// <returns></returns>
        public static object ParseObject(string xmlStr)
        {
            XmlDocument doc = new XmlDocument();
            try
            {
                if (System.IO.File.Exists(xmlStr))
                {
                    doc.Load(xmlStr);
                }
                else
                {
                    doc.LoadXml(xmlStr);
                }
                return parse(doc.DocumentElement);
            }
            catch (Exception ex)
            {
                throw new ApplicationException("XML Format Error:" + ex.Message, ex);
            }
        }

        static object parse(XmlNode node)
        {
            if (node.Attributes!=null && node.Attributes[TAG_TYPE_NAME] != null && node.Attributes[TAG_TYPE_NAME].Value == TYPE_LIST)
            {
                return parseList(node.ChildNodes);
            }
            //基本類型(葉子結點)必須里面是一個字符串,不能在這里面有注釋,因為注釋也是一個childNode
            else if (!node.HasChildNodes || node.ChildNodes.Count == 1 && node.ChildNodes[0].NodeType == XmlNodeType.Text && node.InnerText == node.ChildNodes[0].OuterXml)
            {
                return node.InnerText;
            }
            else //if (node.HasChildNodes && !(node.ChildNodes.Count == 1 && node.ChildNodes[0].NodeType == XmlNodeType.Element))
            {
                return parseDic(node.ChildNodes);
            }
        }

        static ArrayList parseList(XmlNodeList nodes)
        {
            ArrayList ret = new ArrayList();
            foreach(XmlNode node in nodes)
            {
                if (node.NodeType == XmlNodeType.Element)
                {
                    ret.Add(parse(node));
                }
            }
            return ret;
        }

        static Dictionary<string, object> parseDic(XmlNodeList nodes)
        {
            Dictionary<string, object> ret = new Dictionary<string, object>();
            foreach(XmlNode node in nodes)
            {
                if (node.NodeType == XmlNodeType.Element)
                {
                    ret.Add(node.Name, parse(node));
                }
            }
            return ret;
        }
    }
}
