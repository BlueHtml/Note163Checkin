# Note163Checkin

## 一、Fork 仓库

**点击右上角的`Fork`**
![Fork](https://img.guoqianfan.com/note/2020/08/fork.png)

## 二、添加 Secret

**`Settings`->`Secrets`->`New secret`，添加以下Secret：**
- `Conf`：其值如下：
    ```json
    {
    	"Users": [{
    			"Task": "CC", //自定义名字，选填
    			"Username": "abc@163.com", //账号
    			"Password": "aaa" //密码
    		}, {
    			"Task": "MM",
    			"Username": "123@163.com",
    			"Password": "111"
    		}
    	],
    	"ScKey": "", //server酱sckey，不填不开启
    	"ScType": "Failed" //通知类型. Always:始终通知; Failed:失败时通知; 不填/其他:不通知;
    }
    ```

**步骤图示如下：**
![添加Secret](https://img.guoqianfan.com/note/2020/08/添加secret.png)

## 三、启用 Action

**点击`Actions`，再点击`I understand my workflows, go ahead and enable them`**
![启用Action](https://img.guoqianfan.com/note/2020/08/启用action.png)

**注意**：Fork 的仓库上的 GitHub Actions 的**定时任务不会自动执行**，必须要手动触发一次后才能正常工作。

所以 Fork 之后，点击自己仓库右上角的`Star`，`Star`你的仓库，这是为了触发 Github Action 第一次执行，之后就会自动执行定时任务。
![Star](https://img.guoqianfan.com/note/2020/08/star.png)

## 四、查看运行结果

**`Actions`->`Run`->`build`**，能看到下图，表示运行成功（注意：由于 .NET Core会输出默认日志，请**滚动到最下面查看实际运行结果**）
![查看Action运行记录](https://img.guoqianfan.com/note/2020/08/查看action运行记录.png)

## 注意事项

24小时内频繁登录可能会触发验证，程序就会登录失败。此时需要在网页上手动登录一次（需要输入验证码），登录成功后再次运行本程序即可。

每天运行一次，在上午9:00-9:45之间。

也可以点击右上角的`Star`手动运行。
![Star](https://img.guoqianfan.com/note/2020/08/star.png)

## 参考

参考了以下项目：
- [ydao](https://github.com/yygtboy/ydao/)
- [node-script](https://github.com/SunSeekerX/node-script)
- [youdaoyun](https://github.com/hezhizheng/youdaoyun)
